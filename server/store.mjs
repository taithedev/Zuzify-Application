import { DatabaseSync } from 'node:sqlite';
import { randomBytes,createHash,scryptSync,timingSafeEqual } from 'node:crypto';
import { plan } from './plans.mjs';
export const db=new DatabaseSync(process.env.ZUZIFY_DB||'./zuzify.sqlite');
db.exec(`PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS accounts(id TEXT PRIMARY KEY,username TEXT UNIQUE NOT NULL,password TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS sessions(hash TEXT PRIMARY KEY,account TEXT NOT NULL REFERENCES accounts(id),expires INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS licenses(hash TEXT PRIMARY KEY,plan TEXT NOT NULL,expires INTEGER,account TEXT REFERENCES accounts(id),revoked INTEGER NOT NULL DEFAULT 0,source TEXT UNIQUE);
CREATE TABLE IF NOT EXISTS events(id TEXT PRIMARY KEY);`);
export const digest=s=>createHash('sha256').update(s).digest('hex');
export const random=()=>randomBytes(32).toString('base64url');
export function password(value,salt=randomBytes(16).toString('hex')){return salt+':'+scryptSync(value,salt,64).toString('hex');}
export function check(value,stored){const expected=Buffer.from(stored);const actual=Buffer.from(password(value,stored.split(':')[0]));return expected.length===actual.length&&timingSafeEqual(expected,actual);}
export function session(account){const token=random();db.prepare('INSERT INTO sessions VALUES(?,?,?)').run(digest(token),account,Date.now()+86400000);return token;}
export function identity(token){return db.prepare('SELECT a.id,a.username FROM sessions s JOIN accounts a ON a.id=s.account WHERE s.hash=? AND s.expires>?').get(digest(token||''),Date.now());}
export function entitlements(id){return db.prepare('SELECT plan,expires FROM licenses WHERE account=? AND revoked=0 AND (expires IS NULL OR expires>?)').all(id,Date.now());}
export function issue(id,expires=null){if(!plan(id))throw Error('Unknown plan');if(id==='monthly'&&!expires)expires=Date.now()+30*86400000;const key='ZUZIFY-'+randomBytes(24).toString('hex').toUpperCase();db.prepare('INSERT INTO licenses(hash,plan,expires) VALUES(?,?,?)').run(digest(key),id,expires);return key;}
export function redeem(id,key){return db.prepare('UPDATE licenses SET account=? WHERE hash=? AND account IS NULL AND revoked=0 AND (expires IS NULL OR expires>?)').run(id,digest(key.trim().toUpperCase()),Date.now()).changes===1;}
