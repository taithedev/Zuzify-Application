export const plans=Object.freeze({monthly:{name:"Monthly",cents:2500,monthly:true},standard:{name:"Standard",cents:4500},pro:{name:"Pro",cents:8500},elite:{name:"Elite",cents:12500},ultimate:{name:"Ultimate",cents:20000}});
export function plan(id){return Object.hasOwn(plans,id)?plans[id]:null;}
