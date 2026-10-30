"""Sets every 3D texture's import options for a shipped game: lossy colour (WebP, quality 0.85) and
VRAM-compressed normal maps, with mipmaps. The editor would do this itself the first time it saw a
texture used in 3D; this project is built headless, so it never did, and every texture went into the
game lossless and without mipmaps (twice the download, and shimmering at a distance).
Palettes (KayKit), portraits and the sky keep their settings. Run from the repo root, then
`godot --headless --path game --import`."""
import pathlib, re, sys
root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'game/assets')
keep = ('kaykit/', 'polyhaven/sky/')
changed = 0
for p in root.rglob('*.import'):
    text = p.read_text()
    if 'importer="texture"' not in text or any(k in p.as_posix() for k in keep): continue
    name = p.name.lower()
    normal = '_nor' in name or 'normal' in name
    want = {
        'compress/mode': '2' if normal else '1',
        'compress/normal_map': '1' if normal else '0',
        'compress/lossy_quality': '0.85',
        'compress/high_quality': 'false',
        'mipmaps/generate': 'true',
        'detect_3d/compress_to': '0',
    }
    new = text
    for key, val in want.items():
        new, n = re.subn(rf'^{re.escape(key)}=.*$', f'{key}={val}', new, flags=re.M)
        if n == 0: new = new.rstrip('\n') + f'\n{key}={val}\n'
    if new != text:
        p.write_text(new)
        changed += 1
print(f'{changed} texture imports set for 3D')
