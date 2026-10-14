// Build the private, offline design review from maintained source and supplied assets.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const version = process.argv[2] || 'v2';
if (!['v2','v3'].includes(version)) throw new Error('Choose v2 or v3');
const source = path.join(root, 'design/review-'+version);
const output = path.join(root, version === 'v2' ? 'outputs/Design-lab' : 'outputs/Design-lab-v3');
const assets = path.join(output, 'assets');
fs.mkdirSync(output, { recursive: true });
if(version==='v3')fs.cpSync(path.join(root,'outputs/Design-lab/assets'),assets,{recursive:true,force:false});
for (const file of ['index.html','review.css','review.js']) fs.copyFileSync(path.join(source,file),path.join(output,file));
let html = fs.readFileSync(path.join(source,'index.html'),'utf8');
html = html.replace('<link rel="stylesheet" href="review.css">','<style>'+fs.readFileSync(path.join(source,'review.css'),'utf8')+'</style>');
html = html.replace('<script src="review.js"></script>','<script>'+fs.readFileSync(path.join(source,'review.js'),'utf8')+'</script>');
const embedded={};
for (const file of ['collection-reference.png','ac4-reference.png','for-answer-reference.png']) embedded[file]=fs.readFileSync(path.join(assets,file)).toString('base64');
// One Blob URL per image keeps the embedded bytes out of repeated DOM attributes.
const assetsScript='const embeddedImages='+JSON.stringify(embedded)+';const embeddedUrls=Object.fromEntries(Object.entries(embeddedImages).map(([name,bytes])=>[name,URL.createObjectURL(new Blob([Uint8Array.from(atob(bytes),c=>c.charCodeAt(0))],{type:"image/png"}))]));function assetUrl(name){return embeddedUrls[name];}';
html=html.replace("function assetUrl(name){return 'assets/'+name;}",assetsScript);
html=html.replaceAll('assets/Cinzel.ttf','data:font/ttf;base64,'+fs.readFileSync(path.join(assets,'Cinzel.ttf')).toString('base64'));
const license='data:text/plain;charset=utf-8,'+encodeURIComponent(fs.readFileSync(path.join(assets,'Cinzel-OFL.txt'),'utf8'));
html=html.replaceAll('href="assets/Cinzel-OFL.txt" target="_blank"','href="'+license+'" download="Cinzel-OFL.txt"');
fs.writeFileSync(path.join(root,'outputs/Design-review-'+version+'.html'),html);
console.log('Built '+output+' and offline Design-review-'+version+'.html');
