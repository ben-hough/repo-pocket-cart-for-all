import zipfile,json,re,io,hashlib,urllib.request,sys
from PIL import Image
ok=True
def chk(c,msg):
    global ok
    print(('  PASS ' if c else '  FAIL ')+msg); ok&=bool(c)
def dep_exists(ns,name,ver):
    req=urllib.request.Request(f'https://thunderstore.io/api/experimental/package/{ns}/{name}/{ver}/',headers={'User-Agent':'Mozilla/5.0 MrGlim-validate'})
    try: d=json.load(urllib.request.urlopen(req)); return d.get('is_active',False)
    except Exception as e: print('   ',e); return False
for z in sys.argv[1:]:
    n=re.search(r'MrGlim-(\w+)-1\.0\.0\.zip',z)[1]; print('==',z)
    zf=zipfile.ZipFile(z); names=zf.namelist(); print('  entries:',names)
    chk(zf.testzip() is None,'zip CRC ok')
    chk(all('\\' not in x for x in names),'forward-slash paths only')
    chk(all(not x.endswith('/') for x in names),'no directory entries / extra folders')
    chk({'manifest.json','icon.png','README.md'}<=set(names),'manifest.json, icon.png, README.md at root')
    chk('CHANGELOG.md' in names,'CHANGELOG.md at root')
    chk(f'MrGlim.{n}.dll' in names,'DLL at root (past accepted convention)')
    chk(not any(x.lower().endswith(('.pdb','.mdb')) for x in names),'no pdb/mdb')
    raw=zf.read('manifest.json'); chk(not raw.startswith(b'\xef\xbb\xbf'),'manifest has no BOM')
    m=json.loads(raw.decode('utf-8')); print('  manifest:',json.dumps(m))
    for f in ('name','version_number','website_url','description','dependencies'): chk(f in m,f'manifest field {f}')
    chk(re.fullmatch(r'[A-Za-z0-9_]+',m['name']) and len(m['name'])<=128 and m['name']==n,'name matches ^[A-Za-z0-9_]+$')
    chk(m['version_number']=='1.0.0','version 1.0.0')
    chk(len(m['description'])<=250,f"description {len(m['description'])} <= 250 chars")
    chk(m['website_url']=='','website_url empty (no fake repo)')
    for dep in m['dependencies']:
        mm=re.fullmatch(r'([A-Za-z0-9_]+)-([A-Za-z0-9_]+)-(\d+\.\d+\.\d+)',dep); chk(mm,f'dep format {dep}')
        chk(dep_exists(*mm.groups()),f'dep {dep} exists and is active on Thunderstore')
    ib=zf.read('icon.png'); im=Image.open(io.BytesIO(ib))
    chk(im.format=='PNG' and im.size==(256,256),f'icon PNG {im.size} {im.mode}, {len(ib)} bytes')
    r=zf.read('README.md'); txt=r.decode('utf-8'); chk(not r.startswith(b'\xef\xbb\xbf'),'README UTF-8, no BOM')
    chk('## AI disclosure' in txt,'README has AI disclosure')
    dll=zf.read(f'MrGlim.{n}.dll'); chk(b'AI_Assisted_Creation' in dll and b'AI_Model_Vendor' in dll,'DLL has AI AssemblyMetadata')
    print('  dll md5', hashlib.md5(dll).hexdigest())
print('ALL PASS' if ok else 'SOME FAILED')
