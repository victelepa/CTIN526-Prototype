using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Coloured boxes built from a generated 1x1 sprite, so whitebox geometry and
    /// runtime HUD elements need no imported art. The material is explicitly unlit:
    /// URP's 2D renderer hands new sprites a lit material, which renders black in a
    /// scene with no Light2D.
    /// </summary>
    public static class PrimitiveSprite
    {
        public enum ColliderKind
        {
            None,
            Solid,
            Trigger
        }

        static Sprite _unitSprite;
        static Material _unlitMaterial;

        public static Sprite UnitSprite
        {
            get
            {
                if (_unitSprite != null) return _unitSprite;

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                _unitSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _unitSprite.name = "PrimitiveUnit";
                return _unitSprite;
            }
        }

        public static Material UnlitMaterial
        {
            get
            {
                if (_unlitMaterial != null) return _unlitMaterial;

                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                _unlitMaterial = new Material(shader) { name = "PrimitiveUnlit" };
                return _unlitMaterial;
            }
        }

        public static SpriteRenderer CreateBox(string name, Transform parent, Vector2 center, Vector2 size, Color color,
            ColliderKind collider = ColliderKind.None, int sortingOrder = 0)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = UnitSprite;
            renderer.sharedMaterial = UnlitMaterial;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (collider != ColliderKind.None)
            {
                BoxCollider2D shape = box.AddComponent<BoxCollider2D>();
                shape.size = Vector2.one;
                shape.isTrigger = collider == ColliderKind.Trigger;
            }

            return renderer;
        }
    }
}
