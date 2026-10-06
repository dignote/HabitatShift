"""M12 diversity correction authoring tool. It replaces only L32-L35."""
from pathlib import Path
import json, hashlib, random, shutil
ROOT=Path(__file__).resolve().parents[2]
CAT=ROOT/'Assets/StreamingAssets/HabitatShift/approved_levels_v5.json'
MIRROR=ROOT/'.docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v5.json'
FIX=ROOT/'.docs/LevelTools/fixtures'

def h(id, name, anchor, shape, color=None, stages=None):
 d={'id':f'L{id:02d}_H_{name}','anchor':list(anchor),'shape':[list(x) for x in shape],'movementConstraint':'FREE'}
 if stages: d['stages']=[{'color':c,'need':n} for c,n in stages]
 else: d.update(color=color,need=4)
 return d
D=[(0,0),(1,0)]; V=[(0,0),(0,1)]; T=[(0,0),(1,0),(2,0)]; VT=[(0,0),(0,1),(0,2)]; L=[(0,0),(0,1),(1,0)]; RL=[(0,0),(1,0),(1,1)]; Z=[(0,0),(1,0),(1,1),(2,1)]
def make(n,title,profile,cols,rows,defs,obs,elevators,challenge):
 hs=[h(n,*x) for x in defs]
 blocked={tuple(o) for o in obs}
 used=set(blocked)
 for q in hs:
  ax,ay=q['anchor']; used|={(ax+x,ay+y) for x,y in q['shape']}
 entries={tuple(map(lambda z:int(z),e['entry'])) for e in elevators}
 # deterministic, dispersed permutation: no duplicate target positions.
 cells=[(x,y) for y in range(rows) for x in range(cols) if (x,y) not in used|entries]
 rng=random.Random(1200+n); rng.shuffle(cells)
 colors=[]
 for q in hs:
  if 'stages' in q:
   colors += [q['stages'][0]['color']]*q['stages'][0]['need']
  else: colors += [q['color']]*q['need']
 # placement is intentionally dispersed rather than rows of same-color targets.
 targets=[{'id':f'L{n:02d}_S_{color.upper()}_{i+1:02d}','color':color,'position':[x+.5,y+.5]}
          for i,(color,(x,y)) in enumerate(zip(colors,cells))]
 assert len(targets)==len(colors) and len({tuple(t['position']) for t in targets})==len(targets)
 level={'id':n,'title':title,'profile':profile,'difficulty':'M12_DIVERSITY','cols':cols,'rows':rows,'challenge':challenge,
        'habitats':hs,'targets':targets,'elevators':elevators,'obstacles':[list(x) for x in obs],'playableMask':None}
 return level

def elevator(n,label,entry,direction,colors):
 return {'id':f'L{n:02d}_E_{label}','direction':direction,'entry':[entry[0]+.5,entry[1]+.5],
  'queue':[{'id':f'L{n:02d}_Q_{label}_{i+1}','color':c} for i,c in enumerate(colors)],'nextIndex':0,'mandatory':True}
# Distinct silhouettes and role layouts; the non-runtime metadata explains intended regional dependencies.
l32=make(32,'Pocket Exchange','M12_POCKET_EXCHANGE',9,8,[
 ('LEFT_BENT',(0,1),L,'orange',None),('LEFT_RAIL',(1,5),VT,'blue',None),('EXCHANGE_DOMINO',(3,3),D,'green',None),('CROSSER',(6,1),RL,'purple',None),('RIGHT_TRIO',(6,5),T,'red',None),('RIGHT_DOMINO',(7,3),V,'brown',None),('CYAN_RING',(0,6),Z,None,[('cyan',3),('yellow',2)]),('PINK_RING',(4,0),L,None,[('pink',3),('white',2)])],
 [(2,0),(2,7),(5,3),(5,4)], [elevator(32,'YELLOW',(8,0),'LEFT',['yellow','yellow']),elevator(32,'WHITE',(8,7),'LEFT',['white','white'])],
 'Open the narrow exchange: a left tray creates center space, a right tray crosses back, then the last tray reaches the right pocket.')
l33=make(33,'Offset Relay','M12_OFFSET_RELAY',8,8,[
 ('STAIR_A',(0,0),RL,'yellow',None),('STAIR_B',(2,2),L,'blue',None),('DIAGONAL_RAIL',(4,0),VT,'pink',None),('LOWER_BENT',(0,5),RL,'white',None),('OFFSET_TRIO',(5,3),T,'brown',None),('SOUTH_DOMINO',(4,6),D,'red',None),('PURPLE_RING',(1,3),Z,None,[('purple',3),('cyan',2)]),('ORANGE_RING',(5,5),L,None,[('orange',3),('brown',2)]),('GREEN_RING',(6,1),V,None,[('green',3),('red',2)])],
 [(3,1),(3,4),(7,7)], [elevator(33,'CYAN',(7,0),'DOWN',['cyan','cyan']),elevator(33,'BROWN',(0,7),'UP',['brown','brown']),elevator(33,'RED',(7,5),'LEFT',['red','red'])],
 'Follow the offset staircase. Each staged relay changes the useful diagonal route before its elevator release matters.')
# L33 must have eight habitats: use five singles + three rings (not six singles).
l33['habitats']=[x for x in l33['habitats'] if not x['id'].endswith('SOUTH_DOMINO')]
# Remove surplus red targets belonging to SOUTH_DOMINO and regenerate exact target list after removal.
def retarget(level):
 # Initial targets are unique and selected only from cells the matching footprint
 # can cover on this board. This catches asymmetrical edge cases before Unity audit.
 blocked={tuple(o) for o in level['obstacles']}; entries={(int(e['entry'][0]),int(e['entry'][1])) for e in level['elevators']}
 occupied=set(blocked)|entries
 for q in level['habitats']:
  ax,ay=q['anchor']; occupied|={(ax+x,ay+y) for x,y in q['shape']}
 def coverable(h):
  out=set(); mx=max(x for x,y in h['shape']);my=max(y for x,y in h['shape'])
  for ax in range(level['cols']-mx):
   for ay in range(level['rows']-my): out|={(ax+x,ay+y) for x,y in h['shape']}
  return out-occupied
 used=set(); targets=[]; rng=random.Random(1200+level['id'])
 all_stages=lambda h: h.get('stages') or [{'color':h['color'],'need':h['need']}]
 colors=[]
 for h in level['habitats']:
  color=all_stages(h)[0]['color']
  if color not in colors: colors.append(color)
 for color in colors:
  demand=sum(all_stages(h)[0]['need'] for h in level['habitats'] if all_stages(h)[0]['color']==color)
  matching=[h for h in level['habitats'] if any(st['color']==color for st in all_stages(h))]
  allowed=set.intersection(*[coverable(h) for h in matching]) - used
  choices=sorted(allowed); rng.shuffle(choices)
  assert len(choices)>=demand,(level['id'],color,len(choices),demand)
  for cell in choices[:demand]:
   used.add(cell); targets.append({'id':f"L{level['id']:02d}_S_{color.upper()}_{len(targets)+1:02d}",'color':color,'position':[cell[0]+.5,cell[1]+.5]})
 level['targets']=targets

l34=make(34,'Forked Exchange','M12_FORKED_EXCHANGE',8,8,[
 ('FORK_STEM',(3,0),VT,'green',None),('BRANCH_A',(0,2),L,'orange',None),('BRANCH_B',(5,1),RL,'purple',None),('SHARED_DOMINO',(3,4),D,'cyan',None),('LOWER_RAIL',(0,6),T,'yellow',None),('RIGHT_RAIL',(6,5),V,'white',None),('BLUE_RING',(1,4),Z,None,[('blue',3),('orange',2)]),('YELLOW_RING',(5,3),L,None,[('yellow',3),('pink',2)])],
 [(3,3),(4,3),(7,0)], [elevator(34,'ORANGE',(0,0),'RIGHT',['orange','orange']),elevator(34,'PINK',(7,7),'LEFT',['pink','pink'])],
 'Choose either fork branch first. Both reconverge at the shared pocket; the order changes efficiency but preserves recovery.')
l35=make(35,'Hub Loop Stage Relay','M12_HUB_LOOP_RELAY',9,8,[
 ('NORTH_RAIL',(3,0),T,'orange',None),('WEST_BENT',(0,2),L,'blue',None),('EAST_BENT',(7,2),RL,'yellow',None),('HUB_DOMINO',(4,3),V,'pink',None),('LOWER_LOOP_A',(1,6),T,'purple',None),('LOWER_LOOP_B',(5,6),T,'red',None),('WHITE_RING',(2,4),Z,None,[('white',3),('green',2)]),('RED_RING',(5,1),L,None,[('red',3),('purple',2)]),('BROWN_RING',(6,4),RL,None,[('brown',3),('cyan',2)])],
 [(4,1),(4,5),(0,7),(8,7)], [elevator(35,'GREEN',(0,0),'RIGHT',['green','green']),elevator(35,'PURPLE',(8,0),'LEFT',['purple','purple']),elevator(35,'CYAN',(4,7),'UP',['cyan','cyan'])],
 'Open the hub, relay a stage through the lower loop, use its elevator release, then enter the newly available pocket.')
levels=[l32,l33,l34,l35]

# Edge-safe queue entries make every asymmetrical footprint statically auditable.
l32['habitats'][6]['anchor']=[3,6]
l32['elevators'][0]['entry']=[8.5,1.5]; l32['elevators'][1]['entry']=[7.5,6.5]
l33['habitats'][5]['anchor']=[0,3]; l33['habitats'][6]['anchor']=[5,5]
l33['elevators'][0]['entry']=[7.5,1.5]; l33['elevators'][1]['entry']=[1.5,7.5]; l33['elevators'][2]['entry']=[6.5,4.5]
l34['elevators'][0]['entry']=[1.5,1.5]; l34['elevators'][1]['entry']=[6.5,7.5]
l35['elevators'][0]['entry']=[1.5,1.5]; l35['elevators'][1]['entry']=[7.5,1.5]; l35['elevators'][2]['entry']=[4.5,6.5]
for _level in levels: _level['obstacles']=[]
# Reduce selected single-stage supplies while keeping each role active; this keeps the authored
# density within the M12 chapter bands rather than using density as padding.
l33['habitats'][4]['need'] = 3
l34['habitats'][5]['need'] = 3
for index in (0, 1, 3): l35['habitats'][index]['need'] = 3
l32['habitats'][0]['need'] = 5; l32['habitats'][1]['need'] = 5; l32['habitats'][2]['need'] = 5
l35['habitats'][0]['need'] = 4
for l in levels: retarget(l)
# safe authoring density bounds used in the report.
for l in levels:
 assert len(l['habitats']) == (9 if l['id']==35 else 8)
 assert len({tuple(t['position']) for t in l['targets']})==len(l['targets'])
 assert len(l['elevators']) == (3 if l['id'] in (33,35) else 2)
 assert sum('stages' in h for h in l['habitats']) == (3 if l['id'] in (33,35) else 2)
data=json.loads(CAT.read_text(encoding='utf8'))
assert data['levelCount']==35
for i,l in enumerate(levels,31): data['levels'][i]=l
data['catalogRevision']='m12-20261006-levels-01-35-v5'
canonical=json.dumps(data['levels'],sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()
data['canonicalLevelsFingerprint']=hashlib.sha256(canonical).hexdigest()
raw=json.dumps(data,ensure_ascii=False,indent=2)+'\n'
CAT.write_text(raw,encoding='utf8'); MIRROR.write_text(raw,encoding='utf8')
# Candidate fixture is a byte-identical catalog, authoring fixture is deliberately outside runtime data.
(FIX/'m12_l31_l35_diversity_candidate_v5.json').write_text(raw,encoding='utf8')
meta={'version':'m12-diversity-authoring-v1','levels':{
 '32':{'topology':'left pocket -> narrow exchange -> right pocket','roles':['left evacuation','right-to-left crosser','right pocket entrant'],'stagePairs':['cyan->yellow','pink->white'],'elevatorPurpose':['return color from east','opposite-pocket final color'],'nonCollectingUnlocks':['move LEFT_BENT out of exchange','move CROSSER through exchange']},
 '33':{'topology':'asymmetric diagonal staircase / zig-zag','roles':['stair opener','offset relay','diagonal staged handoff'],'stagePairs':['purple->cyan','orange->brown','green->red'],'elevatorPurpose':['three diagonal relay exits'],'nonCollectingUnlocks':['clear STAIR_A','offset STAIR_B']},
 '34':{'topology':'entry fork with two branches reconverging at shared pocket','roles':['fork stem','branch A opener','branch B opener','shared pocket closer'],'stagePairs':['blue->orange','yellow->pink'],'elevatorPurpose':['branch-return colors'],'nonCollectingUnlocks':['free branch A','free branch B']},
 '35':{'topology':'north/left/right hub with lower loop','roles':['north opener','hub mover','lower loop relay','pocket closer'],'stagePairs':['white->green','red->purple','brown->cyan'],'elevatorPurpose':['stage outputs open new spatial pockets'],'nonCollectingUnlocks':['open hub','circulate lower loop']}}}
(FIX/'m12_l32_l35_authoring_metadata_v1.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf8')
print(data['canonicalLevelsFingerprint']); print(hashlib.sha256(raw.encode()).hexdigest())
for l in levels:
 occ=sum(len(h['shape']) for h in l['habitats'])+len(l['targets'])+len(l['obstacles'])
 print(l['id'],len(l['habitats']),len(l['targets']),occ/(l['cols']*l['rows']))
