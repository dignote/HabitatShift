#if UNITY_EDITOR
namespace HabitatShift.LevelLabR10 { public sealed class R10Selection { public string kind,id; public bool Has { get { return !string.IsNullOrEmpty(id); } } public void Set(string k,string value){kind=k;id=value;} public void Clear(){kind=id=null;} } }
#endif