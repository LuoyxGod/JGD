using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace JGD.Level
{
    [CreateAssetMenu(menuName = "JGD/Puzzle Registry", fileName = "PuzzleRegistry")]
    public class PuzzleRegistry : ScriptableObject
    {
        [Serializable]
        public class PrefabEntry
        {
            [LabelText("拼图名(唯一键)")] public string pieceName;
            [LabelText("建模 Prefab")] public GameObject prefab;      // 空 = 未建模
            [LabelText("建模时地块大小")] public float modeledTileSize = 50f;
            [LabelText("已完成")] public bool done;
        }

        [LabelText("关卡数据")] public LevelData level = new LevelData();

        [TableList(ShowPaging = true)]
        [LabelText("拼图条目")]
        public List<PrefabEntry> entries = new List<PrefabEntry>();

        public PrefabEntry GetEntry(string pieceName)
        {
            return entries.Find(e => e.pieceName == pieceName);
        }

        public GameObject GetPrefab(string pieceName)
        {
            var e = GetEntry(pieceName);
            return e != null ? e.prefab : null;
        }

        public float ModeledTileSize(string pieceName)
        {
            var e = GetEntry(pieceName);
            return e != null && e.modeledTileSize > 0 ? e.modeledTileSize : 0f;
        }

        // 导入 JSON 后调用：按 pieces 重建条目，同名条目保留已指定的 Prefab 引用
        public void EnsureEntries(LevelData data)
        {
            level = data;
            var fresh = new List<PrefabEntry>();
            foreach (var p in data.pieces)
            {
                var old = GetEntry(p.name);
                if (old != null) { old.modeledTileSize = old.modeledTileSize > 0 ? old.modeledTileSize : data.cfg.tileSize; fresh.Add(old); }
                else fresh.Add(new PrefabEntry { pieceName = p.name, modeledTileSize = data.cfg.tileSize });
            }
            entries = fresh;
        }
    }
}
