"""Rebuild factual catalog from cached authoritative source snapshots; no game bytes."""
import json, re, pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
R=ROOT/'work/research'
def evidence(url,conclusion,confidence='high'):
    return dict(url=url,conclusion=conclusion,confidence=confidence,accessed='2026-10-09')
PS1='https://github.com/stenzek/duckstation/blob/master/data/resources/gamedb.yaml'
PS2='https://github.com/PCSX2/pcsx2/blob/master/bin/resources/GameIndex.yaml'
PSP='https://github.com/libretro/libretro-database/blob/master/metadat/serial/Sony%20-%20PlayStation%20Portable.dat'
RPC='https://rpcs3.net/compatibility?api=v1&g=Armored'
ANALOG='https://github.com/VanLaser/analog-controls-for-armored-core-ps2'
games=[
 ('ac1','Armored Core','Armored Core',1,1997,False,'The original Raven contract. Classic digital controls; analog modifications are optional.','AC'),
 ('acpp','Armored Core: Project Phantasma','Project Phantasma',1,1997,False,'A first-generation expansion with compatible original-game save import.','PP'),
 ('acmoa','Armored Core: Master of Arena','Master of Arena',1,1999,False,'Two-disc arena and campaign collection. Register each disc separately.','MA'),
 ('ac2','Armored Core 2','Armored Core 2',2,2000,False,'The series moves to PlayStation 2. Keep automatic game fixes enabled.','02'),
 ('ac2aa','Armored Core 2: Another Age','Another Age',2,2001,False,'A mission-focused follow-up with Armored Core 2 save carryover.','AA'),
 ('ac3','Armored Core 3','Armored Core 3',3,2002,False,'The layered world of the third generation. PS2 and Portable remain distinct editions.','03'),
 ('acsl','Silent Line: Armored Core','Silent Line',3,2003,False,'A separate campaign that supports importing Armored Core 3 data.','SL'),
 ('acn','Armored Core: Nexus','Nexus',3,2004,False,'Evolution and Revolution are separate discs. Native dual-stick controls.','NX'),
 ('acnb','Armored Core: Nine Breaker','Nine Breaker',3,2004,False,'A training and arena release. Keep its own edition and configuration.','NB'),
 ('acff','Armored Core: Formula Front','Formula Front',3,2004,False,'PS2 and PSP variants differ. Choose the release you own; do not substitute platforms.','FF'),
 ('aclr','Armored Core: Last Raven','Last Raven',3,2005,False,'The final PS2 campaign. Scaling can affect post-processing; Original is the default.','LR'),
 ('ac4','Armored Core 4','Armored Core 4',4,2006,True,'PS3 and Xbox 360 are independent options. Regulations and updates change balance.','04'),
 ('acfa','Armored Core: For Answer','For Answer',4,2008,True,'Fast fourth-generation combat. Preserve both console editions and independent build selections.','FA'),
 ('acv','Armored Core V','Armored Core V',5,2012,True,'Experimental emulation support. Do not assume campaign completion or online availability.','V'),
 ('acvd','Armored Core: Verdict Day','Verdict Day',5,2013,True,'Experimental emulation support with documented rendering issues.','VD')]
c=dict(schemaVersion=1,version='1.2.1',games=[dict(zip(['id','title','shortTitle','generation','year','optional','description','mark'],g)) for g in games],editions=[],backends=[],profiles=[],enhancements=[],patches=[],extensions=[],firmware=[],providers=[])
presentation={
 'acpp':('Armored Core','Project Phantasma'), 'acmoa':('Armored Core','Master of Arena'),
 'ac2aa':('Armored Core 2','Another Age'), 'acsl':('Armored Core','Silent Line'),
 'acn':('Armored Core','Nexus'), 'acnb':('Armored Core','Nine Breaker'),
 'acff':('Armored Core','Formula Front'), 'aclr':('Armored Core','Last Raven'),
 'acfa':('Armored Core','for Answer'), 'acvd':('Armored Core','Verdict Day')}
for game in c['games']:
 game['displayTitle'],game['displaySubtitle']=presentation.get(game['id'],(game['title'],''))
backend_info=[
 ('duckstation','DuckStation','ps1','stenzek/duckstation','CC-BY-NC-ND-4.0','latest',r'^duckstation-windows-x64-release\.zip$','duckstation-qt-x64-ReleaseLTCG.exe','ps1-bios','LICENSE'),
 ('pcsx2','PCSX2','ps2','PCSX2/pcsx2','GPL-3.0','stable',r'windows-x64-Qt\.7z$','pcsx2-qt.exe','ps2-bios','COPYING.GPLv3'),
 ('ppsspp','PPSSPP','psp','hrydgard/ppsspp','GPL-2.0-or-later','stable',r'Windows-x64\.zip$','PPSSPPWindows64.exe','none','LICENSE.TXT'),
 ('rpcs3','RPCS3','ps3','RPCS3/rpcs3-binaries-win','GPL-2.0-only','rolling',r'win64_msvc\.7z$','rpcs3.exe','ps3-firmware','LICENSE'),
 ('xenia','Xenia Canary','x360','xenia-canary/xenia-canary','BSD-3-Clause','canary',r'windows\.7z$','xenia_canary.exe','none','LICENSE')]
formats={'ps1':['cue','bin','iso','chd','img','pbp'],'ps2':['iso','bin','cue','chd','cso','mdf'],'psp':['iso','cso','pbp'],'ps3':['ps3-folder','ps3-iso'],'x360':['iso','xex','stfs','x360-folder']}
for id,name,platform,repo,lic,channel,pattern,exe,firmware,licfile in backend_info:
    rel=json.loads((R/(id+'-release.txt')).read_text(encoding='utf-8-sig'))
    a=next(a for a in rel['assets'] if re.search(pattern,a['name'],re.I))
    source_repo='RPCS3/rpcs3' if id=='rpcs3' else repo
    recommended=dict(version=rel['name'],tag=rel['tag_name'],uri=a['browser_download_url'],sha256=a['digest'].removeprefix('sha256:'),fileName=a['name'],published=rel['published_at'],provenance='Official upstream archive and GitHub SHA-256, captured 2026-10-09. Gameplay not locally certified.')
    c['backends'].append(dict(id=id,name=name,platform=platform,projectUrl='https://github.com/'+source_repo,license=lic,releaseEndpoint='https://api.github.com/repos/'+repo+('/releases/tags/latest' if id=='duckstation' else '/releases/latest'),assetPattern=pattern,executable=exe,channel=channel,firmwareClass=firmware,formats=formats[platform],recommended=recommended,licenseUrl='https://github.com/'+source_repo+'/blob/'+('canary_experimental' if id=='xenia' else 'master')+'/'+licfile,arguments=[],updatePolicy='manual-immutable'))

next(b for b in c['backends'] if b['id']=='rpcs3')['minimumBuildsByFormat']={'ps3-iso':'0.0.43-20250'}

def game_id(title):
    s=title.lower()
    for word,id in [('phantasma','acpp'),('arena','acmoa'),('another age','ac2aa'),('silent line','acsl'),('nexus','acn'),('nine breaker','acnb'),('formula front','acff'),('last raven','aclr'),('for answer','acfa'),('verdict day','acvd')]:
        if word in s:return id
    if re.search(r'core\s*2',s):return 'ac2'
    if re.search(r'core\s*3',s):return 'ac3'
    if re.search(r'core\s*4',s):return 'ac4'
    if re.search(r'core\s*v',s):return 'acv'
    return 'ac1'
def add_edition(gid,platform,region,serial,title,source,disc=1,title_id=None,warnings=None):
    eid=gid+'-'+platform+'-'+re.sub('[^a-z0-9]','',serial.lower())+('-'+region.lower() if platform=='x360' else '')
    if any(e['id']==eid for e in c['editions']):return
    backend=next(b['id'] for b in c['backends'] if b['platform']==platform)
    profile=eid+'-conservative'
    family=({'ac1':'gen1','acpp':'gen1','acmoa':'gen1','ac2':'gen2','ac2aa':'gen2','ac3':'gen3','acsl':'gen3','acn':'nexus','acnb':'nexus','aclr':'nexus'}.get(gid,gid))+'-'+platform+'-'+region.lower()
    if platform=='psp':family=('portable3' if gid in ['ac3','acsl','aclr'] else 'formula')+'-'+region.lower()
    structures={'ps1':['disc image or CUE with all tracks'],'ps2':['ISO9660 SYSTEM.CNF and boot executable'],'psp':['PSP_GAME/PARAM.SFO, PSP_GAME/SYSDIR/EBOOT.BIN'],'ps3':['PS3_GAME/PARAM.SFO','PS3_GAME/USRDIR/EBOOT.BIN'],'x360':['XEX2 executable, XDVDFS image or STFS container']}
    warnings=list(warnings or [])
    if platform in ['ps3','x360']:warnings+=['Title updates and regulation data are edition-specific; none are silently downloaded.','Online play requires separate community setup and is not guaranteed.']
    c['editions'].append(dict(id=eid,gameId=gid,platform=platform,region=region,serial=serial,titleId=title_id,release=title,formats=formats[platform],preferredProfile=profile,alternateProfiles=[],saveFamily=family,structure=structures[platform],hashes=[],warnings=warnings,sources=[evidence(source,'Product identity and edition evidence; no exact retail dump hash supplied.')],disc=disc))
    settings={
      'ps1':{'Main/ApplyCompatibilitySettings':'true','Main/ApplyGameSettings':'true','Main/EnableDiscordPresence':'false','GPU/ResolutionScale':'1','GPU/PGXPEnable':'false'},
      'ps2':{'EmuCore/EnablePatches':'true','EmuCore/EnableGameFixes':'true','EmuCore/EnableCheats':'false','EmuCore/GS/upscale_multiplier':'1','EmuCore/GS/AspectRatio':'4:3'},
      'psp':{'Graphics/InternalResolution':'1','Graphics/FrameSkip':'0','System/FastMemoryAccess':'false','General/EnableCheats':'false'},
      'ps3':{'Video/Renderer':'Vulkan','Video/Resolution':'1280x720','Video/Resolution Scale':'100','Video/Write Color Buffers':'true','Miscellaneous/Start games in fullscreen mode':'true','Net/Connection status':'Disconnected'},
      'x360':{'GPU/gpu':'d3d12','General/discord':'false'}
    }[platform].copy()
    notes=[]
    if platform=='ps1':notes=['Preserves digital-era controls. Analog mods are opt-in. Keep upstream compatibility settings enabled.']
    if platform=='ps2':
        notes=['Uses PCSX2 automatic GameIndex fixes; do not disable them.']
        if gid in ['ac2','ac2aa']:notes+=['Upstream applies EE clamping and blending fixes for AI and clouds.']
        if gid in ['acn','acnb','acff','aclr']:notes+=['Upstream handles water mipmaps, blur alignment, and border glow. Higher scaling can expose post-processing artifacts.']
    if platform=='psp':notes=['Preserves this Portable release. No PS2 save conversion. Original controls; analog executable patches remain opt-in.']
    if platform=='ps3':notes=['Upstream Playable classification is not a guarantee on this PC.' if gid in ['ac4','acfa'] else 'Upstream classification: Ingame, not Playable. Rendering and progression issues may remain.']
    if platform=='x360':notes=['Experimental backend; GPU/driver regressions are possible. Retain previous builds. No local gameplay certification.']
    if gid=='acvd' and platform=='ps3':notes+=['Recent RPCS3 issue #19555 reports blue booster-light corruption; status may change.']
    c['profiles'].append(dict(id=profile,editionId=eid,backendId=backend,version='1.0.0',testedBuild=None,testStatus='upstream-informed-not-locally-tested',settings=settings,requiredPatches=[],optionalPatches=[],enhancements=[profile+'-'+x for x in ['original','enhanced','high']],defaultEnhancement=profile+'-original',warnings=notes,sources=[evidence(source,'Conservative baseline informed by upstream. Requires local gameplay verification.','medium')],inputSupport='controller',mouseSupport='future-extension'))
    if platform=='x360' and gid=='acv' and region=='US':
        # Required guest-memory behavior, not a Windows security setting. Scope to
        # the edition exercised locally; do not infer other editions are certified.
        p=c['profiles'][-1]
        p['version']='1.0.2'
        p['testStatus']='locally-sampled'
        p['testedBuild']='828014b_canary_experimental'
        p['settings']['Memory/protect_zero']='false'
        p['settings']['GPU/readback_resolve']='full'
        p['warnings'].append('Memory and texture fixes passed the opening tutorial. Full texture readback can reduce performance on slower hardware. Later missions remain untested.')
        p['sources'].append(dict(url='https://github.com/xenia-project/game-compatibility/issues/280',conclusion='Requires protect_zero=false. Local Canary 828014b90 comparison passed the previously stalled first-mission load with this setting alone; no complete-game certification.',confidence='high',accessed='2026-10-10'))
        p['sources'].append(dict(url='https://github.com/xenia-canary/xenia-canary/blob/828014b90/src/xenia/gpu/command_processor.cc',conclusion='Full readback restored the remaining black hand after fast readback fixed the mech and terrain. User confirmed textures and audio; a 30-second sample stayed at 30 FPS on Ryzen 9800X3D / RTX 5080. Sampled tutorial only.',confidence='high',accessed='2026-10-10'))
    scale_key={'ps1':'GPU/ResolutionScale','ps2':'EmuCore/GS/upscale_multiplier','psp':'Graphics/InternalResolution','ps3':'Video/Resolution Scale'}.get(platform)
    for name,level in [('original',1),('enhanced',2),('high',3)]:
        # Unvalidated later-generation scaling remains native, explicitly explained rather than pretending max graphics is safe.
        scale=level if platform in ['ps1','psp'] or platform=='ps2' and gid in ['ac2','ac2aa','ac3','acsl'] else 1
        override={scale_key:str(scale*100 if platform=='ps3' else scale)} if scale_key else {}
        c['enhancements'].append(dict(id=profile+'-'+name,name={'original':'Original','enhanced':'Enhanced','high':'High-End'}[name],profileId=profile,settings=override,benefit='Native output and conservative effects.' if scale==1 else f'{scale}x internal resolution for clearer geometry.',drawback='Higher tiers remain native until game-specific scaling is validated.' if scale==1 and level>1 else 'Texture detail is unchanged; scaling may expose seams.' if scale>1 else 'Lower output resolution.',hardwareCost='Native GPU cost.' if scale==1 else f'Approximately {scale*scale}x pixel work; hardware-dependent.',risk='Low configuration risk; gameplay untested here.' if scale==1 else 'Optional scaling; return to Original if effects look wrong.'))
    if platform=='x360' and gid=='acv' and region=='US':
        experimental=profile+'-60fps-experimental'
        c['profiles'][-1]['enhancements'].append(experimental)
        c['enhancements'].append(dict(id=experimental,name='60 FPS target (experimental)',profileId=profile,settings={'GPU/vsync':'false','GPU/framerate_limit':'120'},benefit='Smoother movement and camera, retaining the full texture fix.',drawback='Cutscenes, AI, boss attacks and later missions are not validated at this rate. Return to Original if timing changes.',hardwareCost='About twice the frame workload. Opening-scene sample: 58.3 FPS on Ryzen 9800X3D / RTX 5080.',risk='Experimental timing change. Opening movement and audio felt normal to the tester; broader correctness is unconfirmed.'))

def blocks(path):
    data=path.read_text(encoding='utf-8-sig')
    return re.findall(r'^([A-Z]{4}-\d+):\n(.*?)(?=^[A-Z]{4}-\d+:|\Z)',data,re.M|re.S)
for platform,file,url in [('ps1','duckstation-gamedb.txt',PS1),('ps2','pcsx2-index.txt',PS2)]:
    for serial,block in blocks(R/file):
        names=re.findall(r'^  name(?:-en)?: "([^"]+)"',block,re.M)
        title=next((s for s in names if 'Armored' in s),None)
        if not title or 'Demo' in title:continue
        region='US' if serial.startswith(('SCUS','SLUS')) else 'EU' if serial.startswith(('SCES','SLES')) else 'JP' if serial.startswith(('SLPS','SLPM')) else 'ASIA'
        disc=2 if re.search(r'disc 2',title,re.I) else 1
        add_edition(game_id(title),platform,region,serial,title,url,disc)
for gid,region,serial,title in [('ac3','US','NPUH-10023','Armored Core 3 Portable'),('ac3','JP','ULJM-05492','Armored Core 3 Portable'),('acsl','US','NPUH-10025','Silent Line Portable'),('acsl','JP','ULJM-05552','Silent Line Portable'),('acsl','EU','NPEH-00047','Silent Line Portable'),('aclr','US','NPUH-10024','Last Raven Portable'),('aclr','JP','ULJM-05611','Last Raven Portable'),('acff','US','ULUS-10034','Formula Front Extreme Battle'),('acff','EU','ULES-00219','Formula Front Extreme Battle'),('acff','JP','ULJS-19001','Formula Front International')]:
    add_edition(gid,'psp',region,serial,title,PSP if serial.startswith(('ULJM','ULUS','ULES')) else ANALOG,warnings=['Portable/International variants are separate from PS2; content and controls differ.'])
compat=json.loads((R/'rpcs3-armored.txt').read_text(encoding='utf-8-sig'))['results']
for serial,data in compat.items():
    if serial.startswith(('BLES','BLUS')):
        add_edition(game_id(data['title']),'ps3','US' if serial.startswith('BLUS') else 'EU',serial,data['title'],RPC,title_id=serial,warnings=[f"Official compatibility classification {data['status']}, report {data['date']}, commit {data['commit']}."])
for gid,serial in [('ac4','BLJM60012'),('acfa','BLJM60066'),('acv','BLJM60378'),('acvd','BLJM61014')]:
    add_edition(gid,'ps3','JP',serial,next(g[1] for g in games if g[0]==gid),'https://wiki.rpcs3.net/index.php?title=Armored_Core_4' if gid=='ac4' else 'https://ps3.aldostools.org/ird.html',title_id=serial)
for gid,titleid in [('ac4','465307D6'),('acfa','465307DA'),('acv','4E4D084F'),('acvd','4E4D0864')]:
    for region in ['US','JP']:
        source={'ac4':'https://github.com/xenia-project/game-compatibility/issues/279','acfa':'https://github.com/xenia-project/game-compatibility/issues/654','acv':'https://github.com/xenia-manager/optimized-settings','acvd':'https://github.com/xenia-manager/optimized-settings'}[gid]
        add_edition(gid,'x360',region,titleid,next(g[1] for g in games if g[0]==gid),source,title_id=titleid,warnings=['Region cannot be established from shared title ID alone. Confirm the edition manually. Region variants without separate evidence remain provisional.'])

# Reference BIOS hashes from DuckStation's BIOS database, not firmware data.
for label,md5,region in [('SCPH-5501 / 5503, version 3.0','490f666e1afb15b7362b406ed1cea246','US'),('SCPH-5500, version 3.0','8dd7d5296a650fac7319bce665a6a53c','JP'),('SCPH-5502 / 5552, version 3.0','32736f17079d0b2b7024407c39bd3050','EU')]:
    c['firmware'].append(dict(id='ps1-'+region.lower(),name=label,sizes=[524288],hashes=[dict(algorithm='md5',value=md5,release=region,source='https://github.com/stenzek/duckstation/blob/master/src/core/bios.cpp')],guideUrl='https://www.duckstation.org/wiki/BIOS-Dumping',requiredFor='ps1-bios'))
c['firmware'].append(dict(id='ps2-owned',name='Owned PlayStation 2 BIOS',sizes=[4194304,8388608],hashes=[],guideUrl='https://pcsx2.net/docs/usage/setup/',requiredFor='ps2-bios'))
c['firmware'].append(dict(id='ps3-official',name='Official PlayStation 3 system software',sizes=[],hashes=[],guideUrl='https://www.playstation.com/en-us/support/hardware/ps3/system-software/',requiredFor='ps3-firmware',officialUri='http://dus01.ps3.update.playstation.net/update/ps3/image/us/2026_0318_a2b60b6ac1d2e49e230144345616927c/PS3UPDAT.PUP',officialSha256='158471fd834f8ea8036136b6aab43cd86c7ba73d79ca30e0af3c0fe0001cf365'))
c['providers'].append(dict(id='sony-ps3-firmware',kind='OfficialFirmware',priority=0,primaryUri=c['firmware'][-1]['officialUri'],fallbackUris=[],sha256=c['firmware'][-1]['officialSha256'],expectedContentType='application/octet-stream',license='Official Sony end-user download; do not redistribute',allowedHosts=['dus01.ps3.update.playstation.net'],retries=2,maxBytes=300*1024*1024,cachePolicy='immutable'))
# First-class research modules are visible but cannot be enabled without payload permission/hash evidence.
for platform in ['ps1','ps2','psp']:
    editions=[e['id'] for e in c['editions'] if e['platform']==platform and (e['region']=='US' or platform=='psp' and e['serial']=='ULJS-19001')]
    patch=dict(id='vanlaser-analog-'+platform,name='True analog controls · '+platform.upper(),author='VanLaser',version='research',category='Accessibility/Input',upstream=ANALOG,redistribution='unconfirmed',editions=editions,sourceHashes=[],dependencies=[],incompatibilities=[],method='research-only',settings={},payloadSha256=None,providerId=None,benefit='Actual in-game analog processing, not mouse-as-stick.',drawback='Requires exact target patch; replays and behavior may change. License review and target hashes required.',gameplayChanging=True,updateStrategy='explicit')
    c['patches'].append(patch)
    for p in c['profiles']:
        if p['editionId'] in editions:p['optionalPatches'].append(patch['id'])
out=ROOT/'manifests';out.mkdir(exist_ok=True)
(out/'catalog.json').write_text(json.dumps(c,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(f"Catalog: {len(c['games'])} games, {len(c['editions'])} editions, {len(c['profiles'])} profiles, {len(c['enhancements'])} enhancement definitions")
