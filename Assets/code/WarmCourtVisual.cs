using UnityEngine;

// 在背景上放置柔和的活動地毯，讓遊戲空間有溫度但不干擾目標判讀。
public class WarmCourtVisual : MonoBehaviour
{
    [Header("活動地墊尺寸")]
    public float halfWidth = 3.35f;
    public float halfHeight = 3.75f;
    public float lineWidth = 0.055f;

    private Material lineMaterial;
    private Texture2D circleTexture;
    private Sprite circleSprite;

    void Awake()
    {
        CreateActivityMat();
    }

    void CreateActivityMat()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return;

        lineMaterial = new Material(shader);

        Color centerColor = new Color(0.93f, 0.63f, 0.40f, 0.95f);
        Color matColor = new Color(0.98f, 0.84f, 0.67f, 0.88f);

        CreateCircleSprite();
        CreateDisc("ActivityMat", Vector3.zero, 4.15f, matColor, -2);
        CreateDisc("CenterPad", Vector3.zero, 0.72f, centerColor, 0);
        CreateRing("CenterHalo", Vector3.zero, 0.88f, new Color(0.93f, 0.63f, 0.40f, 0.55f), lineWidth, 1);
    }

    void CreateCircleSprite()
    {
        const int textureSize = 128;
        circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.5f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                circleTexture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
    }

    void CreateDisc(string discName, Vector3 position, float radius, Color color, int sortingOrder)
    {
        GameObject discObject = new GameObject(discName);
        discObject.transform.SetParent(transform, false);
        discObject.transform.localPosition = position;
        discObject.transform.localScale = Vector3.one * radius * 2f;

        SpriteRenderer disc = discObject.AddComponent<SpriteRenderer>();
        disc.sprite = circleSprite;
        disc.color = color;
        disc.sortingOrder = sortingOrder;
    }

    void CreateLine(string lineName, Vector3[] points, Color color, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 8;
        line.useWorldSpace = false;
        line.sortingOrder = sortingOrder;
    }

    void CreateRing(string ringName, Vector3 center, float radius, Color color, float width, int sortingOrder)
    {
        const int segmentCount = 32;
        Vector3[] points = new Vector3[segmentCount + 1];

        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = i / (float)segmentCount * Mathf.PI * 2f;
            points[i] = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        CreateLine(ringName, points, color, width, sortingOrder);
    }

    void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);

        if (circleSprite != null)
            Destroy(circleSprite);

        if (circleTexture != null)
            Destroy(circleTexture);
    }
}
