#!/usr/bin/env python3
"""Generate synthetic MUCOMSX playback fixtures and actual Z80 register traces.
Requires Python with the `z80` package, plus the built compiler and msx_driver.
All music and voice fixtures here are synthetic; no commercial songs are included.
"""
from pathlib import Path
import sys,json,re,random,hashlib
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("toolchain",type=Path,help="Built MucoMSX_Toolchain_Work checkout")
parser.add_argument("--output",type=Path,default=Path(__file__).resolve().parents[1]/"EngineSmokeTest/testdata/msx")
args=parser.parse_args()
root=args.toolchain.resolve()
sys.path[:0]=[str(root/'muc2mub/tests'),str(root/'release_tools')]
from native_export_harness import compile_mub
import z80
out=args.output;out.mkdir(parents=True,exist_ok=True)
# Execute the shipped application threshold routine for every Timer-B value.
appbin=(root/'mucplay/bin/mucplay.com').read_bytes()
appsy={n:int(v,16) for n,v in re.findall(r'^(\S+): equ ([0-9A-Fa-f]+)H',(root/'mucplay/bin/mucplay.sym').read_text(),re.M)}
m=z80.Z80Machine();m.set_memory_block(0x100,appbin);m.set_breakpoint(0xff00);thresholds=[]
for timer in range(256):
 m.a=timer;m.pc=appsy['App_ComputeTickThreshold'];m.sp=0xeffe;m.set_memory_block(m.sp,b'\x00\xff');m.ticks_to_stop=100000
 event=m.run()
 if not event&m._BREAKPOINT_HIT or m.pc!=0xff00:raise RuntimeError('App_ComputeTickThreshold')
 thresholds.append(m.hl)
(out/'tick-vectors.json').write_text(json.dumps(dict(mucplay_com_sha256=hashlib.sha256(appbin).hexdigest(),samples=thresholds),indent=2))
voice=bytearray(8192);voice[33:58]=bytes.fromhex('31 73 25 41 29 1f 27 00 5f 1f 1f 9c 08 04 05 1e 03 02 04 06 25 15 25 06 3a');(out/'voice.dat').write_bytes(voice)
wave=b' @W255\n'+b'\n'.join(','.join(str(n*4-128) for n in range(j,j+16)).encode() for j in range(0,64,16))+b'\n'
cases={
'native-psg':b'#title Native PSG\nMSXD @0 v15 t180 o4 l8 cdefgab>c r4\n',
'native-fm':b'#title Native FM\nMSXA @1 v15 t180 o4 l8 cdefgab>c r4\n',
'native-rhythm':b'MSXG v63,31,31,31,31,31,31 @31 t180 l8 c r c r @1 c r\n',
'native-raw':b'MSXA y0,0,191,1 y64,113 y65,97 y66,30 y67,23 y68,239 y69,127 y70,0 y71,23 y112,0 y80,128 y96,21 y87,160 y103,21 y119,16 y135,62 y136,15 y128,100 y129,1 r%48 y96,5 y103,5 y136,0 r%12\n',
'scc-presets':b'#SSGExtend On\nD @0 v15 t180 o4 l16 '+b' '.join(f'@I{n} c'.encode() for n in range(10))+b' pe0,0 g pe8,8 a r4\n',
'scc-user-wave':b'#SSGExtend On\nD @0 @W255 v15 t180 o4 l8 cdef r4\n'+wave,
'mixed-pages':b'#SSGExtend On\nD0 @0 @I8 pe8,8 v15 t180 o4 l8 c r c r\nD1 @0 @I9 pe8,0 v15 o5 l8 r c r c\nE @0 @I3 v13 o3 l8 [c e g]2 r4\nMSXD @0 v13 o4 l8 g e c g r4\nMSXA @1 v15 o5 l8 c e g c r4\n',
'legacy-psg':b'D @0 v15 t180 o4 l8 cdefgab>c r4\n',
'legacy-extended':b'#driver mucomDotNET\nD0 @0 v15 t180 o4 l8 c r c r\nD1 @0 v15 o5 l8 r c r c\n',
'pcm-invert':b'#PCMInvert On\nD @0 v15 t180 o4 l8 cdef r4\n',
}
# raw v2: compact SCC selectors auto-increment across each data write.
wave32=' '.join('y148,'+str(127 if n<16 else 128) for n in range(32))
scc='y142,0 '+wave32+' y143,0 y148,253 y148,0 y144,0 y148,15 y145,0 y148,1 '
plus='y146,128 '+wave32+' y143,8 y148,253 y148,0 y144,4 y148,15 y145,0 y148,16 '
psg='y135,62 y128,100 y129,1 y136,15 '
ssg='y163,117 y164,229 y149,100 y150,1 y156,62 y157,15 y160,5 y161,0 y162,9 y162,9 '
opll='y112,16 y80,128 y96,21 '
for name,flags,commands in [('psg',17,psg),('scc',21,scc),('scc-plus',29,plus),('ssg',17,ssg),('mixed',31,scc+plus+psg+opll)]:
 tail='y145,0 y148,0 y136,0 y157,0 '+('y96,5 ' if flags&2 else '')
 cases['native-raw2-'+name]=f'MSXA C192 l192 t198 y0,0,191,{flags} {commands}r%24 {tail}r%4\n'.encode()
manifest=[]
for name,source in cases.items():
 source=b'#voice voice.dat\n'+source
 raw,_,_=compile_mub(source,voice=voice)
 (out/(name+'.mub')).write_bytes(raw);(out/(name+'.muc')).write_bytes(source)
 manifest.append(dict(name=name,magic=raw[:8].decode(errors='replace'),sha256=hashlib.sha256(raw).hexdigest()))
 print(name,raw[:8],len(raw),flush=True)
(out/'manifest.json').write_text(json.dumps(manifest,indent=2))
sy={m[1]:int(m[2],16) for m in re.finditer(r'^([^:]+): equ ([0-9A-F]+)H',(root/'mubplay/bin/msx_driver.sym').read_text(),re.M)}
bin=(root/'mubplay/bin/msx_driver.bin').read_bytes()
def run_native(writes):
 m=z80.Z80Machine();m.set_memory_block(0x8000,bin);events=[]
 traps={sy['MT_EmitOPLL']:0,sy['MT_EmitPSG']:1,sy['MR_Sink']:3};m.set_breakpoint(0xff00)
 for a in traps:m.set_breakpoint(a)
 def call(label,**regs):
  for k,v in regs.items():setattr(m,k,v)
  m.pc=sy[label];m.sp=0xeffe;m.set_memory_block(m.sp,b'\0\xff')
  for _ in range(1000):
   m.ticks_to_stop=100000;ev=m.run()
   if not ev&m._BREAKPOINT_HIT:continue
   if m.pc==0xff00:return
   events.append([3,m.d,m.c,m.e] if traps[m.pc]==3 else [traps[m.pc],m.d,m.e]);m.pc=int.from_bytes(m.memory[m.sp:m.sp+2],'little');m.sp+=2
  raise RuntimeError(label)
 call('MT_Reset',a=128)
 m.memory[sy['MT_musicReady']]=1
 m.memory[sy['hw_vgmMode']]=1  # MR_Start acquires a virtual SCC lease on demand
 m.set_memory_block(sy['MT_instruments'],b'\1'*6);m.set_memory_block(sy['MT_pan'],b'\xc0'*6)
 for p,r,v in writes:call('MT_Write0' if p==0 else 'MT_Write1',de=(r<<8)|v)
 return events
rng=random.Random(261011);vectors=[]
regs=[0,1,2,3,4,5,6,7,8,9,10,11,12,13,16,17,24,25,26,27,28,29,0x28]+[b+c for b in [0x30,0x40,0x50,0x60,0x70,0x80,0x90,0xa0,0xa4,0xb0,0xb4] for c in range(3)]
for n in range(15):
 writes=[[rng.randrange(2),rng.choice(regs),rng.randrange(256)] for _ in range(100)]
 vectors.append(dict(name=f'assembled-random-{n}',writes=writes,expected=run_native(writes)))
for name,writes in [('raw',[[0,191,1],[0,64,17],[0,127,222],[0,128,3],[0,135,255],[0,141,6],[0,142,55],[0,192,18],[0,255,44],[1,176,88]]),('drum-retrigger',[[0,17,63]]+[[0,24+c,255] for c in range(6)]+[[0,16,31],[0,16,31],[0,16,159],[0,16,0]])]:
 vectors.append(dict(name=name,writes=writes,expected=run_native(writes)))
# Actual raw-v2 SCC ports, independent fifth wave and clock-scaled PSG output.
for flags in (17,19,21,29,31):
 writes=[[0,191,flags],[0,163,117],[0,164,229],[0,149,201],[0,150,1],
         [0,160,255],[0,161,255],[0,155,31],[0,156,255],[0,162,9],[0,162,9],
         [0,142,96],[0,148,127],[0,148,128],[0,146,128],[0,148,42],[0,148,99],
         [0,143,8],[0,148,253],[0,148,2],[0,144,4],[0,148,15],
         [0,145,9],[0,148,16],[0,147,9],[0,148,0],[1,148,127]]
 vectors.append(dict(name=f'assembled-raw2-{flags}',writes=writes,expected=run_native(writes)))
for factor in (1,35781,58741,65535):
 writes=[[0,191,17],[0,163,factor&255],[0,164,factor>>8]]
 writes+=[[0,149+r,rng.randrange(256)] for _ in range(100) for r in [rng.randrange(14)]]
 vectors.append(dict(name=f'assembled-scaled-ssg-{factor}',writes=writes,expected=run_native(writes)))
(out/'mapper-vectors.json').write_text(json.dumps(vectors,indent=2));print('Wrote',len(vectors),'assembled mapper vectors')

sy={m[1]:int(m[2],16) for m in re.finditer(r'^([^:]+): equ ([0-9A-F]+)H',(root/'mubplay/bin/msx_driver.sym').read_text(),re.M)}
blob=(root/'mubplay/bin/msx_driver.bin').read_bytes()
def run_ssg(writes):
 m=z80.Z80Machine();m.set_memory_block(0x8000,blob);events=[]
 m.memory[sy['SD_ready']]=1
 traps={sy['SD_OPNA']:2,sy['SD_Emit']:3};m.set_breakpoint(0xff00)
 for a in traps:m.set_breakpoint(a)
 for _,r,v in writes:
  m.de=(r<<8)|v;m.pc=sy['SD_Write'];m.sp=0xeffe;m.set_memory_block(m.sp,b'\0\xff')
  for _ in range(1000):
   m.ticks_to_stop=100000;ev=m.run()
   if not ev&m._BREAKPOINT_HIT:continue
   if m.pc==0xff00:break
   if traps[m.pc]==2:events.append([2,m.d,m.e])
   else:
    r=m.d;page=4 if r<160 else 1 if r<170 else 2 if r<175 else 3 if r==175 else 5
    addr=r if r<160 else r-160 if r<170 else r-170 if r<175 else 0
    events.append([3,page,addr,m.e])
   m.pc=int.from_bytes(m.memory[m.sp:m.sp+2],'little');m.sp+=2
  else:raise RuntimeError('SD_Write')
 return events
rng=random.Random(411);vectors=json.loads((out/'mapper-vectors.json').read_text())
for n in range(10):
 writes=[[0,rng.randrange(14),rng.randrange(256)]for _ in range(100)]
 vectors.append(dict(name=f'assembled-ssg-{n}',ssg=True,writes=writes,expected=run_ssg(writes)))
(out/'mapper-vectors.json').write_text(json.dumps(vectors,indent=2));print('vectors',len(vectors))

(out/"provenance.json").write_text(json.dumps({"toolchain_driver_sha256":hashlib.sha256(blob).hexdigest(),"generator":"generate-msx-playback-fixtures.py","vectors":len(vectors)},indent=2))
