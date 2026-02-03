// Unity UI stub for CI/local builds — compiled as UnityEngine.UI.dll
namespace UnityEngine.UI {
    public abstract class Graphic : UnityEngine.Behaviour {
        public UnityEngine.Color color { get; set; }
        public bool raycastTarget { get; set; }
        public UnityEngine.RectTransform rectTransform { get; }
        public UnityEngine.Canvas canvas { get; }
        public virtual void SetNativeSize() { }
    }
    public class Image : Graphic {
        public UnityEngine.Sprite sprite { get; set; }
        public Type type { get; set; }
        public bool fillCenter { get; set; }
        public enum Type { Simple, Sliced, Tiled, Filled }
    }
    public class RawImage : Graphic {
        public UnityEngine.Texture texture { get; set; }
        public UnityEngine.Rect uvRect { get; set; }
    }
    public class Text : Graphic {
        public string text { get; set; }
        public UnityEngine.Font font { get; set; }
        public int fontSize { get; set; }
        public UnityEngine.TextAnchor alignment { get; set; }
    }
}
