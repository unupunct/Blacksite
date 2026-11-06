"""Builds the Linux deliverables into output/linux:
   blacksite-linux-x64.tar.gz, blacksite-linux-arm64.tar.gz (self-contained, no .NET install needed) and SHA256SUMS.
Run from any OS:  python scripts/build-linux.py
"""
import hashlib, os, shutil, subprocess, sys, tarfile

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
dotnet = os.path.join(root, '.tools', 'dotnet', 'dotnet.exe')
if not os.path.exists(dotnet):
    dotnet = 'dotnet'
out = os.path.join(root, 'output', 'linux')
proj = os.path.join(root, 'src', 'Blacksite.Linux', 'Blacksite.Linux.csproj')
icon = os.path.join(root, 'src', 'Blacksite.UI', 'Assets', 'logo.png')
shutil.rmtree(out, ignore_errors=True)
os.makedirs(out)
sums = []

for rid, arch in (('linux-x64', 'x64'), ('linux-arm64', 'arm64')):
    pub = os.path.join(out, 'publish-' + arch)
    print('== publish', rid, flush=True)
    r = subprocess.run([dotnet, 'publish', proj, '-c', 'Release', '-r', rid, '--self-contained', 'true', '-o', pub,
                        '-p:DebugType=none', '-p:DebugSymbols=false', '-p:PublishSingleFile=false'])
    if r.returncode:
        sys.exit('publish failed for ' + rid)
    shutil.copy(icon, os.path.join(pub, 'blacksite.png'))
    name = f'blacksite-linux-{arch}.tar.gz'
    path = os.path.join(out, name)
    with tarfile.open(path, 'w:gz', compresslevel=9) as tf:
        for dp, _, files in os.walk(pub):
            for f in sorted(files):
                full = os.path.join(dp, f)
                rel = os.path.join('blacksite', os.path.relpath(full, pub)).replace('\\', '/')
                ti = tf.gettarinfo(full, rel)
                ti.uid = ti.gid = 0; ti.uname = ti.gname = 'root'
                ti.mode = 0o755 if f == 'blacksite' or f.endswith('.so') or '.so.' in f else 0o644
                with open(full, 'rb') as fh:
                    tf.addfile(ti, fh)
    sums.append((hashlib.sha256(open(path, 'rb').read()).hexdigest(), name))
    shutil.rmtree(pub)
    print(f'   {name}: {os.path.getsize(path) / 1048576:.1f} MB')

with open(os.path.join(out, 'SHA256SUMS'), 'w', newline='\n') as f:
    for h, n in sums:
        f.write(f'{h}  {n}\n')
print('done')
