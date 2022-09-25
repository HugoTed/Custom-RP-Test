using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutoffId = Shader.PropertyToID("_Cutoff");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    Color baseColor = Color.white;

    [SerializeField, Range(0, 1)]
    float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

    static MaterialPropertyBlock block;

    //OnValidate在加载或更改组件时在 Unity 编辑器中调用。
    //所以每次加载场景和编辑组件时。因此，各个颜色会立即出现并响应编辑。
    private void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, alphaCutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private void Awake()
    {
        //OnValidate不会在构建中调用
        OnValidate();
    }
}
