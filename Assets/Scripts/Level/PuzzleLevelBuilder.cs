using System.Collections.Generic;
using UnityEngine;

namespace JGD.Level
{
    // 关卡布局生成（编辑器窗口调用，也可在运行时复用）：
    // ① ComputeLayout：统一计算"每张拼图的目标位置"（预览与实体生成共用同一份数据）
    // ② TogglePreviewGO / PreviewDrawer：Scene 视窗画参考网格+区域线框+标签（不生成实体）
    // ③ Build / Clear：生成/清理实体布局（Prefab 或白盒占位 + 出生点柱）
    public static class PuzzleLevelBuilder
    {
        public const string RootName = "PuzzleLevel";
        public const string PreviewName = "PuzzleLevel_Preview";

        public class LayoutItem
        {
            public PieceData piece;
            public int minX, minZ;        // 目标包围盒左下角（格坐标）
            public int wCells, hCells;

            public Vector3 TargetMinWorld(float S)
            {
                return new Vector3(minX * S, 0f, minZ * S);
            }
        }

        // 统一布局：初始图层在原点，其余拼图排在其北侧的"校准排"
        public static List<LayoutItem> ComputeLayout(LevelData level)
        {
            var items = new List<LayoutItem>();
            int cursorX = 0, rowDepth = 0;
            foreach (var p in level.pieces)
            {
                LevelData.Normalize(p, out var nMinX, out var nMinZ);
                int w = 0, h = 0;
                foreach (var c in p.cells) { w = Mathf.Max(w, c.x + 1); h = Mathf.Max(h, c.z + 1); }
                var item = new LayoutItem { piece = p, wCells = w, hCells = h };
                if (p.init)
                {
                    item.minX = 0; item.minZ = 0;
                    rowDepth = Mathf.Max(rowDepth, h);
                }
                else
                {
                    item.minX = cursorX;
                    item.minZ = -(rowDepth + 2 + h);   // 摆在初始图层北侧，留 2 格间隔
                    cursorX += w + 2;
                }
                items.Add(item);
            }
            return items;
        }

        static GameObject _previewGO;
        public static bool HasPreview => _previewGO != null;

        // 返回新建的预览对象；再次调用则关闭并返回 null
        public static GameObject TogglePreviewGO()
        {
            if (_previewGO == null)
            {
                _previewGO = new GameObject(PreviewName);
                _previewGO.hideFlags = HideFlags.DontSave;
                return _previewGO;
            }
            Object.DestroyImmediate(_previewGO);
            _previewGO = null;
            return null;
        }

        public static void ClearPreview()
        {
            if (_previewGO != null) { Object.DestroyImmediate(_previewGO); _previewGO = null; }
        }

        public static void Clear()
        {
            var root = GameObject.Find(RootName);
            if (root != null) Object.DestroyImmediate(root);
        }

        public static GameObject GetRoot()
        {
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            return root;
        }

        // 生成实体布局
        public static void Build(PuzzleRegistry registry, float tileSize, bool showGridLines)
        {
            ClearPreview();
            Clear();
            var rootT = GetRoot().transform;
            var layout = ComputeLayout(registry.level);
            var placeholderMat = MakeSimpleMaterial(new Color(0.85f, 0.85f, 0.92f));
            var spawnMat = MakeSimpleMaterial(new Color(1f, 0.82f, 0.12f));
            var lineMat = MakeSimpleMaterial(new Color(0.55f, 0.55f, 0.6f));

            foreach (var item in layout)
            {
                var p = item.piece;
                float S = tileSize;
                var prefab = registry.GetPrefab(p.name);
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, rootT);
                    go.name = p.name;
                    var modeled = registry.ModeledTileSize(p.name);
                    if (modeled > 0 && Mathf.Abs(modeled - S) > 0.01f)
                        go.transform.localScale *= S / modeled;          // 参数改动 → 等比缩放
                    // 自动对齐：渲染包围盒左下角贴到目标格点
                    var rends = go.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        foreach (var r in rends) b.Encapsulate(r.bounds);
                        var target = item.TargetMinWorld(S);
                        go.transform.position += new Vector3(target.x - b.min.x, 0f, target.z - b.min.z);
                    }
                    else go.transform.position = item.TargetMinWorld(S);
                }
                else
                {
                    // 白盒占位（对齐规则与 Prefab 相同：按格坐标）
                    var group = new GameObject(p.name + "(占位)");
                    group.transform.SetParent(rootT, false);
                    group.transform.position = item.TargetMinWorld(S);
                    foreach (var c in p.cells)
                    {
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = "cell_" + c.x + "_" + c.z;
                        cube.transform.SetParent(group.transform, false);
                        cube.transform.localPosition = new Vector3((c.x + 0.5f) * S, 0.1f, (c.z + 0.5f) * S);
                        cube.transform.localScale = new Vector3(S - 1f, 0.4f, S - 1f);
                        cube.GetComponent<MeshRenderer>().sharedMaterial = placeholderMat;
                    }
                }
                // 出生点标记柱（仅初始图层）
                if (p.init && p.spawn != null)
                {
                    var sm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    sm.name = "Spawn_" + p.name;
                    sm.transform.SetParent(rootT, false);
                    sm.transform.localScale = new Vector3(3f, 0.4f, 3f);
                    sm.transform.localPosition = new Vector3((p.spawn.x + 0.5f) * S, 0.3f, (p.spawn.z + 0.5f) * S);
                    sm.GetComponent<MeshRenderer>().sharedMaterial = spawnMat;
                }
            }

            if (showGridLines) MakeGridLines(rootT, layout, tileSize, lineMat);
        }

        static void MakeGridLines(Transform rootT, List<LayoutItem> layout, float S, Material lineMat)
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var it in layout)
            {
                minX = Mathf.Min(minX, it.minX - 2); minZ = Mathf.Min(minZ, it.minZ - 2);
                maxX = Mathf.Max(maxX, it.minX + it.wCells + 2); maxZ = Mathf.Max(maxZ, it.minZ + it.hCells + 2);
            }
            if (minX > maxX) return;
            var g = new GameObject("GridLines");
            g.transform.SetParent(rootT, false);
            for (int x = minX; x <= maxX; x++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "gx_" + x;
                line.transform.SetParent(g.transform, false);
                line.transform.localScale = new Vector3(0.15f, 0.05f, (maxZ - minZ) * S);
                line.transform.localPosition = new Vector3(x * S, 0.02f, (minZ + (maxZ - minZ) / 2f) * S);
                line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            }
            for (int z = minZ; z <= maxZ; z++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "gz_" + z;
                line.transform.SetParent(g.transform, false);
                line.transform.localScale = new Vector3((maxX - minX) * S, 0.05f, 0.15f);
                line.transform.localPosition = new Vector3((minX + (maxX - minX) / 2f) * S, 0.02f, z * S);
                line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            }
        }

        static Material MakeSimpleMaterial(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (m == null || m.shader == null) m = new Material(Shader.Find("Legacy Shaders/Diffuse"));
            m.color = color;
            return m;
        }
    }
}
