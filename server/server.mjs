import http from 'node:http';
import Stripe from 'stripe';
import { randomUUID } from 'node:crypto';
import { db,digest,random,password,check,session,identity,entitlements,redeem } from './store.mjs';
import { plans,plan } from './plans.mjs';
const key=process.env.STRIPE_SECRET_KEY||'';
if(key&&!key.startsWith('sk_test_'))throw Error('Only Stripe test keys are enabled in this build.');
const stripe=key?new Stripe(key):null;
const webhook=process.env.STRIPE_WEBHOOK_SECRET;
const port=Number(process.env.PORT||8787);
const limit=new Map();
const dummy=password(random());
function json(res,status,body){res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});res.end(JSON.stringify(body));}
async function body(req){let chunks=[],size=0;for await(const chunk of req){size+=chunk.length;if(size>65536)throw Error('Request too large');chunks.push(chunk);}return Buffer.concat(chunks);}
function accountReply(a){return {username:a.username,licenses:entitlements(a.id)};}
function grant(account,id,source,expires){db.prepare('INSERT INTO licenses(hash,plan,expires,account,source) VALUES(?,?,?,?,?) ON CONFLICT(source) DO UPDATE SET expires=excluded.expires,revoked=0').run(digest(random()),id,expires,account,source);}
async function stripeEvent(event){
 if(event.type==='checkout.session.completed'){
  const s=event.data.object;const id=s.metadata?.plan;const account=s.metadata?.account;
  if(!plan(id)||!account||s.livemode||!db.prepare('SELECT id FROM accounts WHERE id=?').get(account))return;
  if(!plans[id].monthly&&s.payment_status==='paid'&&s.amount_total===plans[id].cents&&s.currency==='usd')grant(account,id,'checkout:'+s.id,null);
  if(plans[id].monthly&&s.subscription){const sub=await stripe.subscriptions.retrieve(s.subscription);syncSubscription(sub);}
 }
 if(['customer.subscription.updated','customer.subscription.deleted','customer.subscription.created'].includes(event.type)){const sub=event.data.object;syncSubscription(sub);}
}
function syncSubscription(sub){const account=sub.metadata?.account;const source='subscription:'+sub.id;
 if(!account||sub.livemode||sub.metadata?.plan!=='monthly'||!db.prepare('SELECT id FROM accounts WHERE id=?').get(account))return;
 if(sub.status==='active'&&sub.current_period_end){grant(account,'monthly',source,sub.current_period_end*1000);}
 else db.prepare('UPDATE licenses SET revoked=1 WHERE source=?').run(source);
}
const server=http.createServer(async(req,res)=>{
 try{
 const path=new URL(req.url,'http://localhost').pathname; const method=req.method;
 if(method==='GET'&&path==='/')return json(res,200,{service:'Zuzify',mode:'test',message:'Return to the desktop app and refresh your account.'});
 if(method==='POST'&&path==='/webhook'){
  if(!stripe||!webhook)return json(res,503,{error:'Test billing is not configured'});
  let event;try{event=stripe.webhooks.constructEvent(await body(req),req.headers['stripe-signature'],webhook);}catch{return json(res,400,{error:'Invalid webhook signature'});}
  if(event.livemode)return json(res,400,{error:'Live events not supported'});
  if(!db.prepare('SELECT id FROM events WHERE id=?').get(event.id)){await stripeEvent(event);db.prepare('INSERT OR IGNORE INTO events VALUES(?)').run(event.id);}
  return json(res,200,{received:true});
 }
 if(method==='POST'&&['/register','/login','/redeem','/checkout'].includes(path)){
 const addr=req.socket.remoteAddress||'unknown';const time=Date.now();let entry=limit.get(addr);
 if(!entry||entry.until<time)entry={count:0,until:time+60000};
 entry.count++;limit.set(addr,entry);if(entry.count>20)return json(res,429,{error:'Try again in a minute'});
 }
 const token=(req.headers.authorization||'').replace(/^Bearer /,'');const a=identity(token);
 if(method==='POST'&&['/register','/login'].includes(path)){
  const input=JSON.parse((await body(req)).toString());const u=typeof input.username==='string'?input.username.toLowerCase().trim():'';const p=input.password;
  if(!/^[a-z0-9_]{3,24}$/.test(u)||typeof p!=='string'||p.length<12||p.length>128)return json(res,400,{error:'Use a 3–24 character username and a 12–128 character password'});
  let row=db.prepare('SELECT * FROM accounts WHERE username=?').get(u);
  if(path==='/register'){if(row)return json(res,409,{error:'Username unavailable'});const id=randomUUID();db.prepare('INSERT INTO accounts VALUES(?,?,?)').run(id,u,password(p));row={id,username:u};}
  else if(!check(p,row?.password||dummy)||!row)return json(res,401,{error:'Invalid username or password'});
  return json(res,200,{token:session(row.id),...accountReply(row)});
 }
 if(!a)return json(res,401,{error:'Sign in required'});
 if(method==='GET'&&path==='/me')return json(res,200,accountReply(a));
 if(method==='POST'&&path==='/logout'){db.prepare('DELETE FROM sessions WHERE hash=?').run(digest(token));return json(res,200,{ok:true});}
 if(method==='POST'&&path==='/redeem'){
  const {code}=JSON.parse((await body(req)).toString());if(typeof code!=='string'||code.length>80||!redeem(a.id,code))return json(res,400,{error:'License invalid, expired, revoked or already used'});
  return json(res,200,accountReply(a));
 }
 if(method==='POST'&&path==='/checkout'){
  if(!stripe||!webhook)return json(res,503,{error:'Stripe test billing is not configured'});
  const {plan:id}=JSON.parse((await body(req)).toString());const p=plan(id);if(!p)return json(res,400,{error:'Unknown plan'});
  const metadata={account:a.id,plan:id};const base=process.env.CHECKOUT_RETURN_URL||('http://127.0.0.1:'+port);
  const price_data={currency:'usd',unit_amount:p.cents,product_data:{name:'Zuzify '+p.name},...(p.monthly?{recurring:{interval:'month'}}:{})};
  const s=await stripe.checkout.sessions.create({mode:p.monthly?'subscription':'payment',payment_method_types:['card'],line_items:[{price_data,quantity:1}],metadata,...(p.monthly?{subscription_data:{metadata}}:{}),success_url:base+'/?checkout=success',cancel_url:base+'/?checkout=cancel'});
  return json(res,200,{url:s.url});
 }
 return json(res,404,{error:'Not found'});
 }catch(e){console.error('Request failed:',e?.name);if(!res.headersSent)json(res,400,{error:'Request could not be completed'});else res.end();}
});
setInterval(()=>{for(const [key,v] of limit)if(v.until<Date.now())limit.delete(key);},60000).unref();
server.listen(port,process.env.HOST||'127.0.0.1',()=>console.log('Zuzify license server (test billing) listening on '+port));
