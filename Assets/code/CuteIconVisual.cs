using UnityEngine;

// 以程式繪製簡單的 Q 版中心橘貓與目標毛線球，不影響原有碰撞與目標判定。
public class CuteIconVisual : MonoBehaviour
{
    public enum IconType
    {
        Cat,
        YarnBall
    }

    public IconType iconType;

    private Texture2D texture;
    private Sprite generatedSprite;

    void Awake()
    {
        texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Clear();

        if (iconType == IconType.Cat)
            DrawCat();
        else
            DrawYarnBall();

        texture.Apply();
        generatedSprite = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 128f);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = generatedSprite;
            renderer.color = Color.white;
        }
    }

    void Clear()
    {
        Color[] pixels = new Color[128 * 128];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        texture.SetPixels(pixels);
    }

    void DrawCat()
    {
        Color outline = new Color(0.30f, 0.20f, 0.16f);
        Color orange = new Color(0.94f, 0.48f, 0.20f);
        Color cream = new Color(1f, 0.87f, 0.65f);
        Color blush = new Color(0.94f, 0.48f, 0.48f);

        DrawCircle(64, 42, 32, outline);
        DrawCircle(64, 42, 27, orange);
        DrawTriangle(new Vector2(27, 88), new Vector2(36, 122), new Vector2(56, 94), outline);
        DrawTriangle(new Vector2(72, 94), new Vector2(92, 122), new Vector2(101, 88), outline);
        DrawTriangle(new Vector2(32, 92), new Vector2(38, 114), new Vector2(51, 96), cream);
        DrawTriangle(new Vector2(77, 96), new Vector2(90, 114), new Vector2(96, 92), cream);
        DrawCircle(64, 79, 38, outline);
        DrawCircle(64, 79, 33, orange);
        DrawCircle(50, 80, 5, outline);
        DrawCircle(78, 80, 5, outline);
        DrawCircle(64, 68, 14, cream);
        DrawCircle(56, 68, 10, cream);
        DrawCircle(72, 68, 10, cream);
        DrawCircle(64, 72, 3, outline);
        DrawLine(new Vector2(64, 69), new Vector2(64, 63), outline, 2);
        DrawLine(new Vector2(64, 63), new Vector2(58, 60), outline, 2);
        DrawLine(new Vector2(64, 63), new Vector2(70, 60), outline, 2);
        DrawCircle(39, 69, 5, blush);
        DrawCircle(89, 69, 5, blush);
        DrawCircle(64, 41, 13, cream);
    }

    void DrawYarnBall()
    {
        Color outline = new Color(0.37f, 0.22f, 0.30f);
        Color yarn = new Color(0.78f, 0.36f, 0.50f);
        Color strand = new Color(1f, 0.74f, 0.80f);

        DrawCircle(64, 64, 43, outline);
        DrawCircle(64, 64, 38, yarn);
        DrawLine(new Vector2(30, 52), new Vector2(96, 80), strand, 5);
        DrawLine(new Vector2(34, 78), new Vector2(91, 49), strand, 5);
        DrawLine(new Vector2(43, 30), new Vector2(80, 98), strand, 5);
        DrawLine(new Vector2(28, 65), new Vector2(100, 65), strand, 4);
        DrawCircle(64, 64, 8, new Color(0.88f, 0.49f, 0.61f));
    }

    void DrawCircle(int centerX, int centerY, int radius, Color color)
    {
        int squaredRadius = radius * radius;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy <= squaredRadius)
                    SetPixel(x, y, color);
            }
        }
    }

    void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x));
        int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x));
        int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y));
        int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x, y);
                if (PointInTriangle(point, a, b, c))
                    SetPixel(x, y, color);
            }
        }
    }

    bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float area = Cross(b - a, c - a);
        float first = Cross(b - a, point - a);
        float second = Cross(c - b, point - b);
        float third = Cross(a - c, point - c);
        return area >= 0f ? first >= 0f && second >= 0f && third >= 0f : first <= 0f && second <= 0f && third <= 0f;
    }

    float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    void DrawLine(Vector2 from, Vector2 to, Color color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
            DrawCircle(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), thickness, color);
        }
    }

    void SetPixel(int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            texture.SetPixel(x, y, color);
    }

    void OnDestroy()
    {
        if (generatedSprite != null)
            Destroy(generatedSprite);

        if (texture != null)
            Destroy(texture);
    }
}
