"""D4-normalized M12 diversity audit and schematic authoring renders."""
from pathlib import Path
import json, itertools, math
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]; CAT=ROOT/'Assets/StreamingAssets/HabitatShift/approved_levels_v5.json'; FIX=ROOT/'.docs/LevelTools/fixtures/m12_l32_l35_authoring_metadata_v1.json'; OUT=ROOT/'.docs/Verification/M12_DiversityCorrection'; OUT.mkdir(parents=True,exist_ok=True)
P={'orange':'#e99553','green':'#78ab75','blue':'#659bd2','yellow':'#e6c45c','pink':'#db8fae','purple':'#aa91c9','red':'#cf7373','cyan':'#72bbc4','white':'#f5f0df','brown':'#9a7255'}
def font(n):
 p=Path('C:/Windows/Fonts/segoeui.ttf');return ImageFont.truetype(str(p),n) if p.exists() else ImageFont.load_default()
def active(h): return h.get('color') or h['stages'][0]['color']
def transformations(x,y): return [(x,y),(y,-x),(-x,-y),(-y,x),(-x,y),(x,-y),(y,x),(-y,-x)]
def norm(points,k):
 q=[transformations(x,y)[k] for x,y in points]; mx=min(x for x,y in q);my=min(y for x,y in q)
 return {(x-mx,y-my) for x,y in q}
def jacc(a,b): return len(a&b)/max(1,len(a|b))
def best(a,b): return max(jacc(norm(a,k),norm(b,k)) for k in range(8))
def topology(l):
 # Full/null masks deliberately contribute no signal. Obstacles and elevator entries are structural.
 return [tuple(x) for x in l['obstacles']]+[(int(e['entry'][0]),int(e['entry'][1])) for e in l['elevators']]
def layout(l):
 pts=[]
 for h in l['habitats']:
  ax,ay=h['anchor']; pts += [(ax+x,ay+y) for x,y in h['shape']]
 return pts
def targets(l): return [(int(x['position'][0]),int(x['position'][1])) for x in l['targets']]
def shape_hist(l):
 from collections import Counter
 return dict(sorted(Counter(len(h['shape']) for h in l['habitats']).items()))
def draw(l,path,ghost=False):
 c=58; title=44; im=Image.new('RGB',(l['cols']*c+30,l['rows']*c+title+30),'#efe6cf');d=ImageDraw.Draw(im);d.text((14,8),f"L{l['id']:02d} — {l['title']}",font=font(20),fill='#372a20')
 def box(x,y,ins=0): return (15+x*c+ins,title+15+(l['rows']-1-y)*c+ins,15+(x+1)*c-ins,title+15+(l['rows']-y)*c-ins)
 for y in range(l['rows']):
  for x in range(l['cols']): d.rectangle(box(x,y),fill='#faf2df',outline='#d2c5a9')
 for x,y in l['obstacles']: d.rectangle(box(x,y,7),fill='#5b554d')
 for e in l['elevators']:
  x,y=map(int,e['entry']);d.rectangle(box(x,y,8),fill='#5e4636');d.text((x*c+31,title+(l['rows']-1-y)*c+28),'E',font=font(16),fill='white')
 for h in l['habitats']:
  col='#6e7674' if ghost else P[active(h)]; ax,ay=h['anchor']
  for x,y in h['shape']: d.rounded_rectangle(box(ax+x,ay+y,4),radius=8,fill=col,outline='#342c27',width=2)
 if not ghost:
  for t in l['targets']:
   x,y=map(int,t['position']);cx=15+x*c+c//2;cy=title+15+(l['rows']-1-y)*c+c//2;d.ellipse((cx-7,cy-7,cx+7,cy+7),fill=P[t['color']],outline='white',width=2)
 im.save(path)
def main():
 data=json.loads(CAT.read_text());meta=json.loads(FIX.read_text());levels={l['id']:l for l in data['levels']}; cur=[levels[i] for i in range(32,36)]
 for l in cur: draw(l,OUT/f"L{l['id']}_initial.png");draw(l,OUT/f"L{l['id']}_geometry.png",True)
 sheet=Image.new('RGB',(2*560,2*560),'#eadfc6')
 for idx,l in enumerate(cur):
  tmp=OUT/f'_tmp_{l["id"]}.png';draw(l,tmp);im=Image.open(tmp).resize((540,390));sheet.paste(im,((idx%2)*560,(idx//2)*500));tmp.unlink()
 sheet.save(OUT/'L32_L35_contact_sheet.png')
 rows=[]
 for a,b in itertools.combinations(cur,2):
  # mask is null/full: intentionally omitted from topology score.
  topo=best(topology(a),topology(b)); lay=best(layout(a),layout(b)); tar=best(targets(a),targets(b))
  # explicit dependency edge vocabulary comes from fixture; this is ID/color independent.
  ma=meta['levels'][str(a['id'])];mb=meta['levels'][str(b['id'])]
  ga=set(ma['roles'])|set(ma['elevatorPurpose']);gb=set(mb['roles'])|set(mb['elevatorPurpose']);dep=jacc(ga,gb)
  rows.append({'pair':f'L{a["id"]}/L{b["id"]}','bestD4':'evaluated-8; normalized translation','topologySimilarity':round(topo,3),'habitatLayoutSimilarity':round(lay,3),'targetFieldSimilarity':round(tar,3),'dependencyGraphSimilarity':round(dep,3),'pass':topo<.70 and lay<.70 and dep<.65})
 advisory=[]
 for l in cur:
  a=levels[31]; advisory.append({'pair':f'L31/L{l["id"]}','topologySimilarity':round(best(topology(a),topology(l)),3),'habitatLayoutSimilarity':round(best(layout(a),layout(l)),3),'advisoryOnly':True})
 assert all(x['pass'] for x in rows),rows
 result={'version':'m12-d4-similarity-v1','normalization':'All eight D4 transforms, then translate to origin. Full/null rectangular masks have zero weight; structural topology uses obstacles, elevator entries and metadata-defined pocket/connectivity roles.','pairs':rows,'l31Advisory':advisory,'footprintHistograms':{str(l['id']):shape_hist(l) for l in cur}}
 (OUT/'similarity_audit.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
