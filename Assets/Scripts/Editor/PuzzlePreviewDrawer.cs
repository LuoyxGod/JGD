using UnityEditor;
using UnityEngine;

namespace JGD.Level.EditorTools
{
    // Scene 视窗预览：参考网格 + 各区域线框 + 名字标签 + 出生点标记（不生成实体）
    public class PuzzlePreviewDrawer : MonoBehaviour
    {
        public PuzzleRegistry registry;
        public bool gridLines = true;
        public float tileSize = 50f;

        void OnDrawGizmos()
        {
            if (registry == null || registry.level == null || registry.level.pieces.Count == 0) return;
            var layout = PuzzleLevelBuilder.ComputeLayout(registry.level);
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var it in layout)
            {
                minX = Mathf.Min(minX, it.minX - 2); minZ = Mathf.Min(minZ, it.minZ - 2);
                maxX = Mathf.Max(maxX, it.minX + it.wCells + 2); maxZ = Mathf.Max(maxZ, it.minZ + it.hCells + 2);
            }
            float S = tileSize, y = 0.15f;
            if (gridLines)
            {
                Gizmos.color = new Color(0.45f, 0.5f, 0.6f, 0.9f);
                for (int x = minX; x <= maxX; x++)
                    Gizmos.DrawLine(new Vector3(x * S, y, minZ * S), new Vector3(x * S, y, maxZ * S));
                for (int z = minZ; z <= maxZ; z++)
                    Gizmos.DrawLine(new Vector3(minX * S, y, z * S), new Vector3(maxX * S, y, z * S));
            }
            foreach (var it in layout)
            {
                bool has = registry.GetPrefab(it.piece.name) != null;
                var a = new Vector3(it.minX * S, y, it.minZ * S);
                var b = new Vector3((it.minX + it.wCells) * S, y, (it.minZ + it.hCells) * S);
                Gizmos.color = has ? new Color(0.3f, 0.8f, 0.4f) : new Color(1f, 0.85f, 0.2f);
                Gizmos.DrawLine(a, new Vector3(b.x, y, a.z));
                Gizmos.DrawLine(new Vector3(b.x, y, a.z), b);
                Gizmos.DrawLine(b, new Vector3(a.x, y, b.z));
                Gizmos.DrawLine(new Vector3(a.x, y, b.z), a);
                var center = new Vector3((a.x + b.x) / 2f, 1f, (a.z + b.z) / 2f);
                Handles.Label(center, it.piece.name + (has ? "" : "（占位）"));
                if (it.piece.init && it.piece.spawn != null)
                {
                    Gizmos.color = new Color(1f, 0.82f, 0.12f);
                    Gizmos.DrawCube(new Vector3((it.piece.spawn.x + 0.5f) * S, 0.5f, (it.piece.spawn.z + 0.5f) * S), Vector3.one * 1.2f);
                    Handles.Label(new Vector3((it.piece.spawn.x + 0.5f) * S, 1.4f, (it.piece.spawn.z + 0.5f) * S), "出生点");
                }
            }
        }
    }
}
