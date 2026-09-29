using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;using HabitatShift.Core;using UnityEngine;using UnityEngine.EventSystems;using UnityEngine.UI;
namespace HabitatShift.Runtime {
[Serializable] public class SaveV2 {public int version=2,highest=1,last=1;public bool music=true,sfx=true,haptics=true,vfx=true,reducedMotion=false;public List<int> completed=new List<int>();public List<Best> best=new List<Best>();public List<Charge> charges=new List<Charge>();public RuntimeState snapshot;public int snapshotLevel;}[Serializable] public class LegacyPrefs { public bool Sound=true,Haptics=true,ReducedMotion; } [Serializable] public class Best{public int level,moves;}[Serializable] public class Charge{public int level,nest,garden,trim;}
public class SproutlingMotion:MonoBehaviour
{
    Vector3 basePosition,baseScale;
    float phase; public bool MotionEnabled=true;
    void Awake(){phase=UnityEngine.Random.value*6f;}
    public void SetPose(Vector3 position,Vector3 scale){basePosition=position;baseScale=scale;}
    void Update(){if(!MotionEnabled){transform.localPosition=basePosition;transform.localScale=baseScale;return;}var t=Time.time+phase;transform.localPosition=basePosition+Vector3.up*Mathf.Sin(t*1.4f)*.025f;transform.localScale=baseScale*(1f+Mathf.Sin(t*2f)*.02f);}
}public sealed class HabitatBootstrap:MonoBehaviour{
static HabitatBootstrap instance;CatalogDto catalog;RulesetDto rules;ContinuousSession session;SaveV2 save;Camera cam;Canvas canvas;Sprite white;AudioSource audio;HabitatPresentation presentation;GameplayFxController fx;int current;bool active;string assistHabitat,assistSprout;AssistKind? assist;readonly Dictionary<string,GameObject> world=new Dictionary<string,GameObject>();readonly List<GameObject> ui=new List<GameObject>();readonly Dictionary<int,AudioClip> tones=new Dictionary<int,AudioClip>();Text hud;float winAt=-1,blockedToneAt=-1;int fittedWidth,fittedHeight;Rect fittedSafe;int dragPointerId=int.MinValue;GameObject assistCancelButton;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Create(){if(instance==null)new GameObject("Habitat Shift").AddComponent<HabitatBootstrap>();}
void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);Application.targetFrameRate=60;Screen.orientation=ScreenOrientation.Portrait;StartCoroutine(InitializeCatalogThenHome());}IEnumerator InitializeCatalogThenHome(){CatalogLoadResult loaded=null;yield return CatalogLoader.LoadRoutine(x=>loaded=x);if(loaded==null||!loaded.Success){Debug.LogError("Habitat Shift startup failed closed: "+(loaded==null?"catalog loader returned no result.":loaded.error));yield break;}catalog=loaded.catalog;rules=loaded.rules;save=Load();MakeInfra();ShowHome();}
void MakeInfra(){cam=Camera.main;if(cam==null){cam=new GameObject("Habitat Camera").AddComponent<Camera>();cam.tag="MainCamera";}cam.orthographic=true;cam.backgroundColor=new Color(.16f,.25f,.20f);cam.transform.position=new Vector3(4,-3.5f,-10);audio=gameObject.AddComponent<AudioSource>();PrewarmTones();var t=new Texture2D(2,2);t.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});t.Apply();white=UnityEngine.Sprite.Create(t,new Rect(0,0,2,2),new Vector2(.5f,.5f),2);presentation=gameObject.AddComponent<HabitatPresentation>();presentation.Initialize(cam);fx=gameObject.AddComponent<GameplayFxController>();fx.Initialize(cam);fx.SetSettings(save.vfx,save.reducedMotion);if(FindAnyObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));var c=new GameObject("UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;c.AddComponent<UiLayoutAudit>();var sc=c.GetComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1080,1920);}
void Update(){
    if(active && (fittedWidth!=Screen.width || fittedHeight!=Screen.height || fittedSafe!=Screen.safeArea)) Fit(catalog.levels[current-1]);
    if(active&&session!=null&&!session.Committed.won){
        if(Input.touchCount>0){for(var i=0;i<Input.touchCount;i++)HandleTouch(Input.GetTouch(i));}
        else {if(dragPointerId>=0){EndDrag(true);dragPointerId=int.MinValue;}HandleMouse();}
    }
    if(winAt>0&&Time.unscaledTime>=winAt){winAt=-1;ShowResult();}
}
void HandleMouse(){
    if(Input.GetMouseButtonDown(0)&&dragPointerId==int.MinValue&&!EventSystem.current.IsPointerOverGameObject()){
        StartDrag(Input.mousePosition);if(session.IsDragging)dragPointerId=-1;
    }
    if(dragPointerId!=-1)return;
    if(Input.GetMouseButton(0))Drag(Input.mousePosition);
    if(Input.GetMouseButtonUp(0)){EndDrag();dragPointerId=int.MinValue;}
}
void HandleTouch(Touch touch){
    if(touch.phase==TouchPhase.Began&&dragPointerId==int.MinValue&&!EventSystem.current.IsPointerOverGameObject(touch.fingerId)){
        StartDrag(touch.position);if(session.IsDragging)dragPointerId=touch.fingerId;
    }
    if(dragPointerId!=touch.fingerId)return;
    if(touch.phase==TouchPhase.Moved||touch.phase==TouchPhase.Stationary)Drag(touch.position);
    if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){EndDrag(touch.phase==TouchPhase.Canceled);dragPointerId=int.MinValue;}
}
void CancelDrag(){if(session!=null&&session.IsDragging)EndDrag(true);dragPointerId=int.MinValue;}
void OnGUI(){if(!Debug.isDebugBuild||!active||session==null||!session.IsDragging)return;DebugMarker(session.ResolvedPose,Color.green,"resolved");DebugMarker(session.DesiredPointerPose,Color.yellow,"desired");DebugMarker(session.LastSafePose,Color.cyan,"safe");DebugMarker(session.ResolvedPose+session.GrabOffset,Color.magenta,"grab");if(session.ContactNormal.sqrMagnitude>.001f)DebugMarker(session.ResolvedPose+session.ContactNormal*.35f,Color.red,"contact");}
void DebugMarker(Vector2 board,Color color,string label){var screen=cam.WorldToScreenPoint(BoardViewport.ToWorld(board));var x=screen.x-4;var y=Screen.height-screen.y-4;var old=GUI.color;GUI.color=color;GUI.Box(new Rect(x,y,8,8),GUIContent.none);GUI.Label(new Rect(x+9,y-7,90,20),label);GUI.color=old;}
void StartDrag(Vector2 screen){if(assist.HasValue){AssistTap(ToBoard(screen));return;}var p=ToBoard(screen);var h=session.Committed.habitats.FirstOrDefault(x=>x.shape.Any(c=>p.x>=x.anchor.x+c.x&&p.x<=x.anchor.x+c.x+1&&p.y>=x.anchor.y+c.y&&p.y<=x.anchor.y+c.y+1));if(h!=null&&session.BeginDrag(h.id,p)){Buzz();}}
void Drag(Vector2 s){if(!session.IsDragging)return;session.MovePointer(ToBoard(s));Vector2 settle;presentation.SetSettlePreview(session.DragId,session.TryGetSettlePreview(out settle)?settle:session.ResolvedPose,session.TryGetSettlePreview(out settle));Render(session.Preview);Present();var h=session.Preview.habitats.FirstOrDefault(x=>x.id==session.DragId);if(h!=null)fx.TickDrag(true,h.color,session.ResolvedPose);}void EndDrag(bool cancel=false){if(!session.IsDragging)return;presentation.ClearSettlePreview();session.EndDrag(cancel);Render(session.Committed);Present();SaveSnapshot();UpdateHud();if(session.Committed.won)Victory();}
Vector2 ToBoard(Vector2 s){return BoardViewport.FromScreen(cam,s);}
void StartLevel(int id){current=id;assist=null;assistHabitat=assistSprout=null;dragPointerId=int.MinValue;fx.ClearLevel();fx.SetSettings(save.vfx,save.reducedMotion);var restore=save.snapshotLevel==id?save.snapshot:null;session=new ContinuousSession(catalog.levels[id-1],rules,restore);active=true;winAt=-1;ClearUi();Fit(catalog.levels[id-1]);GameplayUi();Render(session.Committed);if(id==8||id==12||id==16)Tutorial(id);}
void Fit(LevelDto l){BoardViewport.Fit(cam,l);presentation.FitBackdrop(cam);fittedWidth=Screen.width;fittedHeight=Screen.height;fittedSafe=Screen.safeArea;}
void Render(RuntimeState state){var dragging=session!=null&&session.IsDragging;var delta=dragging?session.DesiredPointerPose-session.ResolvedPose:Vector2.zero;presentation.SetPresentationMotion(!save.reducedMotion,dragging?session.DragId:null,delta);presentation.Render(state,catalog.levels[current-1]);}Color ColorFor(string c){switch(c){case "orange":return new Color(.92f,.42f,.31f);case "blue":return new Color(.25f,.48f,.86f);case "green":return new Color(.45f,.67f,.32f);case "yellow":return new Color(.96f,.70f,.18f);case "purple":return new Color(.55f,.39f,.72f);case "cyan":return new Color(.16f,.66f,.67f);case "red":return new Color(.88f,.25f,.38f);case "white":return new Color(.94f,.92f,.84f);default:return new Color(.95f,.44f,.62f);}}
void Present(){fx.SetSettings(save.vfx,save.reducedMotion);UiButtonPulse.ReducedMotion=save.reducedMotion;var events=session.Events;fx.Play(events,catalog.levels[current-1]);foreach(var e in events){if(e.kind==PresentationKind.DragStart)Tone(390,.035f);if(e.kind==PresentationKind.BlockedContact&&Time.unscaledTime-blockedToneAt>.14f){blockedToneAt=Time.unscaledTime;Tone(180,.025f);}if(e.kind==PresentationKind.Collection){Tone(620,.07f);Buzz();}if(e.kind==PresentationKind.HabitatComplete){Tone(820,.12f);Buzz();}if(e.kind==PresentationKind.ElevatorRelease){Tone(740,.08f);Buzz();}if(e.kind==PresentationKind.AssistSuccess){Tone(880,.10f);Buzz();}if(e.kind==PresentationKind.Undo)Tone(330,.045f);}session.Events.Clear();}
void Victory(){active=false;Tone(960,.2f);Buzz();winAt=save.reducedMotion?Time.unscaledTime+.15f:Time.unscaledTime+3.6f;save.completed=save.completed.Union(new[]{current}).ToList();save.highest=Mathf.Max(save.highest,Mathf.Min(CatalogLoader.ExpectedLevelCount,current+1));var b=save.best.FirstOrDefault(x=>x.level==current);if(b==null)save.best.Add(new Best{level=current,moves=session.Committed.moves});else b.moves=Mathf.Min(b.moves,session.Committed.moves);save.snapshot=null;save.snapshotLevel=0;Store();}
void GameplayUi(){
    var root=HudRoot();
    Button(root.transform,"BACK",new Vector2(.08f,.95f),new Vector2(94,94),ShowLevels,HudControlStyle.Circle);
    hud=HudLabel(root.transform,"LEVEL "+current.ToString("00")+" • MOVES 0",new Vector2(.5f,.95f),new Vector2(440,70));
    Button(root.transform,"PAUSE",new Vector2(.92f,.95f),new Vector2(94,94),ShowPause,HudControlStyle.Circle);
    var assists=ActionRow(root.transform,"Assist Row",new Vector2(.5f,.5f),new Vector2(414,122));
    AssistButton(assists.transform,AssistKind.NestBloom,"BLOOM",current>=8);
    AssistButton(assists.transform,AssistKind.GardenShift,"SHIFT",current>=12);
    AssistButton(assists.transform,AssistKind.RootTrim,"TRIM",current>=16);
    assistCancelButton=Button(root.transform,"CLOSE",new Vector2(.5f,.5f),new Vector2(80,80),CancelAssist,HudControlStyle.Circle);
    assistCancelButton.SetActive(false);
    var utilities=ActionRow(root.transform,"Utility Row",new Vector2(.5f,.5f),new Vector2(270,112));
    RowButton(utilities.transform,"UNDO",()=>{if(assist.HasValue)CancelAssist();if(session.Undo()){Render(session.Committed);Present();UpdateHud();SaveSnapshot();}},HudControlStyle.Action,true);
    RowButton(utilities.transform,"RESTART",()=>{if(assist.HasValue)CancelAssist();fx.ClearLevel();session.Restart();Render(session.Committed);UpdateHud();SaveSnapshot();},HudControlStyle.Action,true);
    root.AddComponent<GameplayActionFitter>().Configure(cam,canvas,root.GetComponent<RectTransform>(),
        assists.GetComponent<RectTransform>(),utilities.GetComponent<RectTransform>(),
        assistCancelButton.GetComponent<RectTransform>(),catalog.levels[current-1]);
}
GameObject ActionRow(Transform parent,string name,Vector2 anchor,Vector2 size){var row=new GameObject(name,typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(parent,false);var rect=row.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.sizeDelta=size;var layout=row.GetComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=18;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;return row;}
GameObject RowButton(Transform parent,string label,Action action,HudControlStyle style,bool enabled){var spec=HudTheme.Resolve(label,!enabled);if(style!=spec.Style)spec=new UiControlSpec(style,HudTheme.IconFor(label),UiContentLayout.GameplayAction);var g=new GameObject(label,typeof(Image),typeof(Button),typeof(LayoutElement));g.transform.SetParent(parent,false);HudTheme.StyleButton(g,spec.Style,enabled);var element=g.GetComponent<LayoutElement>();element.minWidth=126;element.preferredWidth=126;element.minHeight=112;element.preferredHeight=112;var button=g.GetComponent<Button>();button.interactable=enabled;g.AddComponent<UiButtonPulse>();button.onClick.AddListener(()=>{if(enabled){Buzz();action();}});HudTheme.AddContent(g,label,spec,enabled,17);return g;}
void AssistButton(Transform parent,AssistKind kind,string label,bool unlocked){var charged=unlocked&&ChargeFor(kind)>0;var button=RowButton(parent,label,()=>ChooseAssist(kind),charged?HudControlStyle.Assist:HudControlStyle.Disabled,charged);Badge(button,unlocked?(Debug.isDebugBuild?"\u221E":ChargeFor(kind).ToString()):"\u2715");}void Badge(GameObject button,string value){var badge=new GameObject("Charge",typeof(Image));badge.transform.SetParent(button.transform,false);var rect=badge.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(1f,1f);rect.anchoredPosition=new Vector2(-19f,-19f);rect.sizeDelta=new Vector2(40,40);HudTheme.StyleButton(badge,HudControlStyle.Circle,true);badge.GetComponent<Image>().raycastTarget=false;var text=new GameObject("Text",typeof(Text));text.transform.SetParent(badge.transform,false);var label=text.GetComponent<Text>();label.text=value;HudTheme.ApplyTypography(label,UiTextStyle.Badge,new Color(.17f,.095f,.055f),TextAnchor.MiddleCenter);label.fontSize=22;label.raycastTarget=false;var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;}
void LevelMark(GameObject card,string icon){var mark=new GameObject("Progress",typeof(Image));mark.transform.SetParent(card.transform,false);var image=mark.GetComponent<Image>();image.sprite=HudTheme.Sprite(icon);image.preserveAspect=true;image.raycastTarget=false;var r=image.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.86f,.78f);r.sizeDelta=new Vector2(33,33);}
void RefreshAssistButtons(){
    var row=canvas!=null?canvas.transform.Find("HUD/Assist Row"):null;
    if(row==null)return;
    var kinds=new[]{AssistKind.NestBloom,AssistKind.GardenShift,AssistKind.RootTrim};
    var unlocks=new[]{8,12,16};
    for(var i=0;i<kinds.Length&&i<row.childCount;i++){
        var item=row.GetChild(i).gameObject;
        var available=current>=unlocks[i]&&ChargeFor(kinds[i])>0;
        var button=item.GetComponent<Button>();button.interactable=available;
        HudTheme.StyleButton(item,available?HudControlStyle.Assist:HudControlStyle.Disabled,available);
        if(available&&assist==kinds[i])item.GetComponent<Image>().color=new Color(.81f,1f,.77f);
        var icon=item.transform.Find("Content/Icon")?.GetComponent<Image>();
        if(icon!=null)icon.color=available?Color.white:new Color(.65f,.67f,.60f,.70f);
        var charge=item.transform.Find("Charge/Text")?.GetComponent<Text>();
        if(charge!=null)charge.text=current<unlocks[i]?"\u2715":Debug.isDebugBuild?"\u221E":ChargeFor(kinds[i]).ToString();
    }
}
void CancelAssist(){
    assist=null;assistHabitat=assistSprout=null;
    presentation.SetAssistCandidates(null);presentation.SetAssistSproutCandidates(null);
    if(assistCancelButton!=null)assistCancelButton.SetActive(false);
    RefreshAssistButtons();
    if(session!=null&&current>0){Render(session.Committed);UpdateHud();}
}
void ChooseAssist(AssistKind k){
    if(assist==k){CancelAssist();return;}
    if(ChargeFor(k)<=0){hud.text="NO ASSIST CHARGES LEFT";return;}
    assist=k;assistHabitat=assistSprout=null;
    presentation.SetAssistCandidates(null);presentation.SetAssistSproutCandidates(null);
    if(k==AssistKind.GardenShift){
        var sprouts=session.Committed.sprouts.Where(x=>session.Committed.habitats.Any(h=>h.color==x.color&&h.remaining>0)).Select(x=>x.id).ToArray();
        if(sprouts.Length==0){CancelAssist();hud.text="NO MATCHING SPROUTLING";return;}
        presentation.SetAssistSproutCandidates(sprouts);hud.text="SHIFT: SELECT SPROUTLING";
    }else{
        var candidates=session.Committed.habitats.Where(h=>{string reason;return session.CanUseAssist(k,h.id,"",out reason);}).Select(h=>h.id).ToArray();
        if(candidates.Length==0){
            var reason=k==AssistKind.RootTrim?"NO TRIMMABLE HABITAT":"NEED MORE SPROUTLINGS";
            if(k==AssistKind.NestBloom&&session.Committed.habitats.Any(h=>h.remaining>0&&
                session.Committed.elevators.Any(e=>e.queue.Skip(e.nextIndex).Any(q=>q.color==h.color))))
                reason="WAIT FOR ELEVATOR";
            CancelAssist();hud.text=reason;return;
        }
        presentation.SetAssistCandidates(candidates);
        hud.text=k==AssistKind.NestBloom?"BLOOM: SELECT HABITAT":"TRIM: SELECT HABITAT";
    }
    if(assistCancelButton!=null)assistCancelButton.SetActive(true);
    RefreshAssistButtons();
    Render(session.Committed);
}
void AssistTap(Vector2 p){
    var h=session.Committed.habitats.FirstOrDefault(x=>x.shape.Any(c=>p.x>=x.anchor.x+c.x&&p.x<=x.anchor.x+c.x+1&&p.y>=x.anchor.y+c.y&&p.y<=x.anchor.y+c.y+1));
    var s=session.Committed.sprouts.FirstOrDefault(x=>(x.position-p).sqrMagnitude<.2f);
    if(assist==AssistKind.NestBloom||assist==AssistKind.RootTrim){
        if(h==null){hud.text="SELECT HIGHLIGHTED HABITAT";return;}
        string reason;if(!session.CanUseAssist(assist.Value,h.id,"",out reason)){hud.text=ShortAssistReason(reason);return;}
        Use(assist.Value,h.id,"",Vector2Int.zero);return;
    }
    if(assistSprout==null&&s!=null){
        assistSprout=s.id;
        var habitats=session.Committed.habitats.Where(x=>x.color==s.color&&x.remaining>0).Select(x=>x.id).ToArray();
        presentation.SetAssistSproutCandidates(null);presentation.SetAssistCandidates(habitats);
        hud.text=habitats.Length==0?"NO HABITAT WITH SPACE":"SHIFT: SELECT HABITAT";
        Render(session.Committed);return;
    }
    if(assistSprout!=null&&h!=null){
        var chosen=session.Committed.sprouts.FirstOrDefault(x=>x.id==assistSprout);
        if(chosen!=null&&h.color==chosen.color){Use(assist.Value,h.id,assistSprout,Vector2Int.zero);return;}
        hud.text="CHOOSE MATCHING HABITAT";
    }
    else if(assistSprout==null)hud.text="SELECT HIGHLIGHTED SPROUTLING";
    else hud.text="SELECT MATCHING HABITAT";
}
string ShortAssistReason(string reason){if(string.IsNullOrEmpty(reason))return "ASSIST UNAVAILABLE";if(reason.Contains("Elevator"))return "WAIT FOR ELEVATOR";if(reason.Contains("Not enough"))return "NEED MORE SPROUTLINGS";if(reason.Contains("origin"))return "ORIGIN CELL BLOCKED";return "HABITAT NOT ELIGIBLE";}
void Use(AssistKind k,string h,string s,Vector2Int leaf){
    var success=session.UseAssist(k,h,s,leaf);
    if(success){Spend(k);Present();SaveSnapshot();}
    CancelAssist();
    if(session.Committed.won)Victory();
}
int ChargeFor(AssistKind k){if(Debug.isDebugBuild)return 999;var c=Ledger();return k==AssistKind.NestBloom?c.nest:k==AssistKind.GardenShift?c.garden:c.trim;}void Spend(AssistKind k){if(Debug.isDebugBuild)return;var c=Ledger();if(k==AssistKind.NestBloom)c.nest--;else if(k==AssistKind.GardenShift)c.garden--;else c.trim--;Store();}Charge Ledger(){var c=save.charges.FirstOrDefault(x=>x.level==current);if(c==null){c=new Charge{level=current,nest=current>=8?1:0,garden=current>=12?1:0,trim=current>=16?1:0};save.charges.Add(c);}return c;}
void UpdateHud(){if(hud!=null)hud.text="LEVEL "+current.ToString("00")+" \u2022 MOVES "+session.Committed.moves;}
void ShowHome(){
    CancelDrag();active=false;ClearWorld();ClearUi();
    var panel=ResponsivePanel("Habitat Shift");
    Label(panel.transform,"HABITAT SHIFT",66);
    var hero=new GameObject("Moss Hero",typeof(Image),typeof(LayoutElement));
    hero.transform.SetParent(panel.transform,false);
    var image=hero.GetComponent<Image>();
    image.sprite=Resources.Load<Sprite>("HabitatShift/sproutling_moss_v1");
    image.preserveAspect=true;image.raycastTarget=false;
    var heroLayout=hero.GetComponent<LayoutElement>();heroLayout.preferredHeight=260;heroLayout.preferredWidth=260;
    Label(panel.transform,"Tiny Friends. Brighter Spaces.",28);
    Button(panel.transform,"CONTINUE",()=>StartLevel(Mathf.Clamp(save.last,1,save.highest)));
    Button(panel.transform,"LEVELS",ShowLevels);
    Button(panel.transform,"SETTINGS",()=>ShowSettings());
}
void ShowLevels(){
    CancelDrag();active=false;ClearWorld();ClearUi();
    var panel=ResponsivePanel("Levels");Label(panel.transform,"SELECT LEVEL",48);
    var viewport=new GameObject("Level Scroll",typeof(Image),typeof(RectMask2D),typeof(ScrollRect),typeof(LayoutElement));
    viewport.transform.SetParent(panel.transform,false);
    var viewportImage=viewport.GetComponent<Image>();viewportImage.color=Color.clear;viewportImage.raycastTarget=true;
    var scrollLayout=viewport.GetComponent<LayoutElement>();scrollLayout.minHeight=240;scrollLayout.preferredHeight=1242;scrollLayout.flexibleHeight=1;scrollLayout.minWidth=0;scrollLayout.preferredWidth=0;scrollLayout.flexibleWidth=1;
    var grid=new GameObject("Level Grid",typeof(RectTransform));
    grid.transform.SetParent(viewport.transform,false);
    var gridRect=grid.GetComponent<RectTransform>();gridRect.anchorMin=new Vector2(0f,1f);gridRect.anchorMax=new Vector2(1f,1f);
    gridRect.pivot=new Vector2(.5f,1f);gridRect.sizeDelta=Vector2.zero;gridRect.anchoredPosition=Vector2.zero;
    var scroll=viewport.GetComponent<ScrollRect>();scroll.content=gridRect;scroll.viewport=viewport.GetComponent<RectTransform>();
    scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
    for(int i=1;i<=CatalogLoader.ExpectedLevelCount;i++){
        var id=i;var locked=id>save.highest;
        var card=Button(grid.transform,locked?"LOCKED":"LEVEL "+id.ToString("00"),()=>{if(!locked)StartLevel(id);},locked);
        if(!locked&&save.completed.Contains(id))LevelMark(card,"icon_check");
        if(!locked&&id==save.last){card.GetComponent<Image>().color=new Color(.89f,1f,.86f);if(!save.completed.Contains(id))LevelMark(card,"icon_assist_bloom");}
    }
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(panel.transform as RectTransform);
    viewport.AddComponent<LevelGridFitter>().Configure(gridRect);
    Button(panel.transform,"HOME",ShowHome);
}
void CloseModal(){for(var i=ui.Count-1;i>=0;i--)if(ui[i]!=null&&ui[i].name.EndsWith(" Backdrop",StringComparison.Ordinal)){Destroy(ui[i]);ui.RemoveAt(i);}}
void ShowPause(){CancelDrag();if(assist.HasValue)CancelAssist();active=false;CloseModal();var p=ResponsivePanel("Pause");Label(p.transform,"PAUSED",54);Button(p.transform,"RESUME",()=>{CloseModal();active=true;Render(session.Committed);});Button(p.transform,"RESTART",()=>{fx.ClearLevel();session.Restart();Render(session.Committed);UpdateHud();SaveSnapshot();CloseModal();active=true;});Button(p.transform,"SETTINGS",()=>ShowSettings(true));Button(p.transform,"LEVEL SELECT",ShowLevels);}
void ShowResult(){ClearUi();var p=ResponsivePanel("Result");Label(p.transform,"LEVEL COMPLETE",54);ModalIcon(p.transform,"icon_check",112);Label(p.transform,session.Committed.moves+" moves",30);Button(p.transform,"NEXT LEVEL",()=>{if(current<CatalogLoader.ExpectedLevelCount)StartLevel(current+1);else ShowLevels();});Button(p.transform,"REPLAY",()=>StartLevel(current));Button(p.transform,"LEVEL SELECT",ShowLevels);}
void Tutorial(int id){active=false;var p=ResponsivePanel("Tutorial");Label(p.transform,"NEW ASSIST",48);ModalIcon(p.transform,id==8?"icon_assist_bloom":id==12?"icon_assist_shift":"icon_assist_trim",118);LongLabel(p.transform,id==8?"Nest Bloom: choose an eligible Habitat to collect all matching Sproutlings already on the board.":id==12?"Garden Shift: choose a Sproutling, then its matching Habitat.":"Root Trim: choose an eligible Habitat to shrink it to its origin cell.");Button(p.transform,"GOT IT",()=>{CloseModal();active=true;});}
void LongLabel(Transform parent,string value){var item=new GameObject("Tutorial Copy",typeof(Text),typeof(LayoutElement));item.transform.SetParent(parent,false);var text=item.GetComponent<Text>();text.text=value;HudTheme.ApplyTypography(text,UiTextStyle.Body,new Color(.23f,.18f,.14f),TextAnchor.MiddleCenter);text.fontSize=26;var layout=item.GetComponent<LayoutElement>();layout.minHeight=150;layout.preferredHeight=150;}
void ShowSettings(bool returnToPause=false){
    active=false;CloseModal();var p=ResponsivePanel("Settings");Label(p.transform,"SETTINGS",48);
    Button(p.transform,"SFX: "+(save.sfx?"ON":"OFF"),()=>{save.sfx=!save.sfx;Store();ShowSettings(returnToPause);});
    Button(p.transform,"HAPTICS: "+(save.haptics?"ON":"OFF"),()=>{save.haptics=!save.haptics;Store();ShowSettings(returnToPause);});
    Button(p.transform,"VFX: "+(save.vfx?"ON":"OFF"),()=>{save.vfx=!save.vfx;fx.SetSettings(save.vfx,save.reducedMotion);Store();ShowSettings(returnToPause);});
    Button(p.transform,"REDUCED MOTION: "+(save.reducedMotion?"ON":"OFF"),()=>{save.reducedMotion=!save.reducedMotion;fx.SetSettings(save.vfx,save.reducedMotion);UiButtonPulse.ReducedMotion=save.reducedMotion;if(session!=null&&current>0)Render(session.Committed);Store();ShowSettings(returnToPause);});
    Button(p.transform,returnToPause?"BACK":"HOME",returnToPause?(Action)ShowPause:ShowHome);
}
void ModalIcon(Transform parent,string icon,float size){var go=new GameObject("Modal Icon",typeof(Image),typeof(LayoutElement));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=HudTheme.Sprite(icon);image.preserveAspect=true;image.raycastTarget=false;var layout=go.GetComponent<LayoutElement>();layout.preferredHeight=size;layout.preferredWidth=size;}
GameObject ResponsivePanel(string name){
    UiButtonPulse.ReducedMotion=save!=null&&save.reducedMotion;
    var root=new GameObject(name+" Backdrop",typeof(Image));
    root.transform.SetParent(canvas.transform,false);
    var background=root.GetComponent<Image>();background.color=new Color(.18f,.13f,.09f,.42f);
    var full=root.GetComponent<RectTransform>();full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=full.offsetMax=Vector2.zero;
    var safe=new GameObject("Safe Area",typeof(RectTransform),typeof(SafeAreaFitter));
    safe.transform.SetParent(root.transform,false);
    var safeRect=safe.GetComponent<RectTransform>();safeRect.anchorMin=Vector2.zero;safeRect.anchorMax=Vector2.one;safeRect.offsetMin=safeRect.offsetMax=Vector2.zero;
    safe.GetComponent<SafeAreaFitter>().Refresh();
    var card=new GameObject(name,typeof(Image),typeof(VerticalLayoutGroup));
    card.transform.SetParent(safe.transform,false);
    HudTheme.StyleButton(card,HudControlStyle.Secondary,true);
    card.GetComponent<Image>().raycastTarget=true;
    card.AddComponent<UiModalPop>().Configure(save!=null&&save.reducedMotion);
    var rect=card.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
    var height=name=="Levels"?1760f:name=="Habitat Shift"?1060f:name=="Settings"?930f:name=="Pause"?900f:1000f;
    card.AddComponent<ModalCardFitter>().Configure(height);
    var layout=card.GetComponent<VerticalLayoutGroup>();
    layout.padding=new RectOffset(50,50,50,50);layout.spacing=18;layout.childAlignment=TextAnchor.MiddleCenter;
    layout.childControlWidth=true;layout.childForceExpandWidth=false;layout.childControlHeight=true;layout.childForceExpandHeight=false;
    ui.Add(root);
    return card;
}
GameObject Panel(string n){UiButtonPulse.ReducedMotion=save!=null&&save.reducedMotion;var root=new GameObject(n+" Backdrop",typeof(Image));root.transform.SetParent(canvas.transform,false);var background=root.GetComponent<Image>();background.color=new Color(.18f,.13f,.09f,.42f);var rr=root.GetComponent<RectTransform>();rr.anchorMin=Vector2.zero;rr.anchorMax=Vector2.one;rr.offsetMin=rr.offsetMax=Vector2.zero;var g=new GameObject(n,typeof(Image),typeof(VerticalLayoutGroup));g.transform.SetParent(root.transform,false);var im=g.GetComponent<Image>();HudTheme.StyleButton(g,HudControlStyle.Secondary,true);im.raycastTarget=true;g.AddComponent<UiModalPop>().Configure(save!=null&&save.reducedMotion);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(820,Mathf.Min(1500,Screen.height*.78f));var v=g.GetComponent<VerticalLayoutGroup>();v.padding=new RectOffset(50,50,70,70);v.spacing=26;v.childAlignment=TextAnchor.MiddleCenter;v.childControlWidth=true;v.childForceExpandWidth=false;v.childControlHeight=true;v.childForceExpandHeight=false;ui.Add(root);return g;}void ClosePanel(GameObject card){if(card==null)return;var root=card.transform.parent!=null?card.transform.parent.gameObject:card;ui.Remove(root);Destroy(root);}GameObject HudRoot(){var g=new GameObject("HUD",typeof(Image),typeof(SafeAreaFitter));g.transform.SetParent(canvas.transform,false);g.GetComponent<Image>().color=Color.clear;g.GetComponent<Image>().raycastTarget=false;var r=g.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;g.GetComponent<SafeAreaFitter>().Refresh();ui.Add(g);return g;}void Label(Transform p,string t,int size){var g=new GameObject("Label",typeof(Text),typeof(LayoutElement));g.transform.SetParent(p,false);var x=g.GetComponent<Text>();x.text=t;HudTheme.ApplyTypography(x,size>=60?UiTextStyle.Display:size>=48?UiTextStyle.Title:size>=32?UiTextStyle.Heading:UiTextStyle.Body,new Color(.23f,.18f,.14f),TextAnchor.MiddleCenter);x.fontSize=size;var e=g.GetComponent<LayoutElement>();e.minHeight=size+40;e.preferredHeight=size+40;}Text HudLabel(Transform p,string t,Vector2 a,Vector2 sz){var g=new GameObject("Hud",typeof(Image));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=a;r.anchoredPosition=Vector2.zero;r.sizeDelta=sz;HudTheme.StylePill(g.GetComponent<Image>());var label=new GameObject("Text",typeof(Text));label.transform.SetParent(g.transform,false);var x=label.GetComponent<Text>();x.text=t;HudTheme.ApplyTypography(x,UiTextStyle.Hud,new Color(.25f,.18f,.14f),TextAnchor.MiddleCenter);x.fontSize=25;var labelRect=x.GetComponent<RectTransform>();labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;return x;}GameObject Button(Transform p,string t,Action a,bool off=false){var spec=HudTheme.Resolve(t,off);if(t=="RESTART"||t=="REPLAY")spec=new UiControlSpec(HudControlStyle.Secondary,HudTheme.IconFor(t),UiContentLayout.Modal);var g=new GameObject(t+" Button",typeof(Image),typeof(Button),typeof(LayoutElement));g.transform.SetParent(p,false);HudTheme.StyleButton(g,spec.Style,!off);g.GetComponent<Button>().interactable=!off;g.AddComponent<UiButtonPulse>();g.GetComponent<Button>().onClick.AddListener(()=>{if(!off){Buzz();a();}});var element=g.GetComponent<LayoutElement>();element.minWidth=720;element.preferredWidth=720;element.preferredHeight=spec.Style==HudControlStyle.Level?192:(spec.IsSetting?116:112);element.minHeight=spec.Style==HudControlStyle.Level?192:(spec.IsSetting?116:112);HudTheme.AddContent(g,t,spec,!off,spec.IsSetting?22:(spec.Style==HudControlStyle.Level?30:28));return g;}
GameObject Button(Transform p,string t,Vector2 a,Vector2 sz,Action f,HudControlStyle style=HudControlStyle.Action,bool enabled=true){var spec=HudTheme.Resolve(t,!enabled);if(style!=HudControlStyle.Action||t=="BACK"||t=="PAUSE")spec=new UiControlSpec(style,HudTheme.IconFor(t),style==HudControlStyle.Circle?UiContentLayout.Circle:UiContentLayout.GameplayAction);var g=new GameObject(t+" HudButton",typeof(Image),typeof(Button));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=a;r.sizeDelta=sz;HudTheme.StyleButton(g,spec.Style,enabled);var button=g.GetComponent<Button>();button.interactable=enabled;g.AddComponent<UiButtonPulse>();button.onClick.AddListener(()=>{if(enabled){Buzz();f();}});HudTheme.AddContent(g,style==HudControlStyle.Circle?"":t,spec,enabled,17);return g;}void ClearUi(){foreach(var x in ui)if(x!=null)Destroy(x);ui.Clear();hud=null;}void ClearWorld(){if(fx!=null)fx.ClearLevel();if(presentation!=null)presentation.Clear();}void SaveSnapshot(){save.last=current;save.snapshotLevel=current;save.snapshot=session.Committed.Clone();Store();}SaveV2 Load(){var p=Path.Combine(Application.persistentDataPath,"habitat_shift_save_v2.json");try{if(File.Exists(p))return JsonUtility.FromJson<SaveV2>(File.ReadAllText(p))??new SaveV2();var old=Path.Combine(Application.persistentDataPath,"habitat_shift_save.json");if(File.Exists(old)){var legacy=JsonUtility.FromJson<LegacyPrefs>(File.ReadAllText(old))??new LegacyPrefs();return new SaveV2{music=legacy.Sound,sfx=legacy.Sound,haptics=legacy.Haptics,reducedMotion=legacy.ReducedMotion};}return new SaveV2();}catch{return new SaveV2();}}void Store(){File.WriteAllText(Path.Combine(Application.persistentDataPath,"habitat_shift_save_v2.json"),JsonUtility.ToJson(save));}void Buzz(){if(save.haptics&&Application.isMobilePlatform)Handheld.Vibrate();}void PrewarmTones(){CacheTone(180,.025f);CacheTone(330,.045f);CacheTone(390,.035f);CacheTone(620,.07f);CacheTone(740,.08f);CacheTone(820,.12f);CacheTone(880,.10f);CacheTone(960,.2f);}
void CacheTone(int hz,float len){var count=Mathf.CeilToInt(22050*len);var clip=AudioClip.Create("Habitat "+hz,count,1,22050,false);var samples=new float[count];for(var i=0;i<count;i++)samples[i]=Mathf.Sin(i*hz*2*Mathf.PI/22050f)*.1f*(1-i/(float)count);clip.SetData(samples,0);tones[hz]=clip;}
void Tone(float hz,float len){if(!save.sfx)return;if(tones.TryGetValue(Mathf.RoundToInt(hz),out var clip))audio.PlayOneShot(clip);}
}}








