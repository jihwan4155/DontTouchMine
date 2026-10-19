using System.Collections.Generic;
using Photon.Voice.Unity;
using UnityEngine;
namespace Game.Network.Players
{
    public sealed class PrivatePlayerVisibility : MonoBehaviour
    {
        private readonly Dictionary<Renderer,bool> renderers=new();
        private readonly Dictionary<AudioSource,bool> sounds=new();
        private readonly Dictionary<Canvas,bool> canvases=new();
        private bool hidden;
        public bool IsHidden => hidden;
        public System.Func<Transform,bool> IncludesChild { get; set; }
        public void SetHidden(bool value)
        {
            if(hidden==value)return;
            hidden=value;
            if(value)Refresh();
            else
            {
                foreach(var p in renderers)if(p.Key!=null)p.Key.forceRenderingOff=p.Value;
                foreach(var p in sounds)if(p.Key!=null)p.Key.mute=p.Value;
                foreach(var p in canvases)if(p.Key!=null)p.Key.enabled=p.Value;
                renderers.Clear();sounds.Clear();canvases.Clear();
            }
        }
        public void RefreshAppearance() => Refresh();
        private void Refresh()
        {
            if(!hidden)return;
            foreach(var r in GetComponentsInChildren<Renderer>(true))
            { if(IncludesChild?.Invoke(r.transform)==false)continue; if(!renderers.ContainsKey(r))renderers.Add(r,r.forceRenderingOff);r.forceRenderingOff=true; }
            foreach(var a in GetComponentsInChildren<AudioSource>(true))
            {
                if(IncludesChild?.Invoke(a.transform)==false)continue;
                // Voice chat is a communication feature, not an avatar action sound.
                if(a.GetComponent<Speaker>()!=null)continue;
                if(!sounds.ContainsKey(a))sounds.Add(a,a.mute);a.mute=true;
            }
            foreach(var c in GetComponentsInChildren<Canvas>(true))
            { if(IncludesChild?.Invoke(c.transform)==false)continue; if(!canvases.ContainsKey(c))canvases.Add(c,c.enabled);c.enabled=false; }
        }
        private void OnTransformChildrenChanged()=>Refresh();
        private void LateUpdate()
        {
            if(!hidden)return;
            foreach(var p in renderers)if(p.Key!=null)p.Key.forceRenderingOff=true;
            foreach(var p in sounds)if(p.Key!=null)p.Key.mute=true;
            foreach(var p in canvases)if(p.Key!=null)p.Key.enabled=false;
        }
        private void OnDisable()=>SetHidden(false);
    }
}
