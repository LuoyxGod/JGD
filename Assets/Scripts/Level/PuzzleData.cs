using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;


namespace JGD.Level
{
    [Serializable]
    public class Cell
    {
        public int x;
        public int z;
        public Cell() { }
        public Cell(int x, int z) { this.x = x; this.z = z; }
    }

    [Serializable]
    public class Edge
    {
        public Cell a = new Cell();
        public Cell b = new Cell();
    }

    [Serializable]
    public class LevelConfig
    {
        public int tileSize = 50;
        public int roadWidth = 8;
        public int doorWidth = 18;
    }

    [Serializable]
    public class PieceData
    {
        public string name = "拼图";
        public string color = "#cccccc";
        public bool init;
        public Cell spawn;                                    // 仅初始图层使用
        public List<Cell> cells = new List<Cell>();
        public List<Edge> roads = new List<Edge>();
        public List<Edge> doors = new List<Edge>();
        public List<Cell> items = new List<Cell>();
    }

    [Serializable]
    public class LevelData
    {
        public int version = 2;
        public LevelConfig cfg = new LevelConfig();
        public List<PieceData> pieces = new List<PieceData>();

        // 编辑器 JSON 的格坐标写作 [x,z] 数组，JsonUtility 读不了，
        // 先把所有 [整数,整数] 预处理成 {"x":..,"z":..} 再解析。
        public static LevelData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 内容为空");
            var fixedJson = Regex.Replace(json, @"\[\s*(-?\d+)\s*,\s*(-?\d+)\s*\]",
                m => "{\"x\":" + m.Groups[1].Value + ",\"z\":" + m.Groups[2].Value + "}");
            var data = JsonUtility.FromJson<LevelData>(fixedJson);
            if (data == null || data.pieces == null || data.pieces.Count == 0)
                throw new ArgumentException("JSON 中没有 pieces 数据");
            var seen = new HashSet<string>();
            foreach (var p in data.pieces)
            {
                if (string.IsNullOrEmpty(p.name)) p.name = "未命名拼图";
                if (!seen.Add(p.name))
                    throw new ArgumentException("存在重名拼图：" + p.name + "（名字是唯一键，请在编辑器中改名后重新导出）");
            }
            return data;
        }

        // 归一化：把 cells/roads/doors/items/spawn 全部平移，使包围盒左下角为 (0,0)
        public static void Normalize(PieceData p, out int minX, out int minZ)
        {
            minX = int.MaxValue; minZ = int.MaxValue;
            foreach (var c in p.cells) { minX = Math.Min(minX, c.x); minZ = Math.Min(minZ, c.z); }
            if (p.cells.Count == 0) { minX = 0; minZ = 0; return; }
            int sx = minX, sz = minZ;
            void Shift(Cell c) { c.x -= sx; c.z -= sz; }
            p.cells.ForEach(Shift);
            foreach (var e in p.roads) { Shift(e.a); Shift(e.b); }
            foreach (var e in p.doors) { Shift(e.a); Shift(e.b); }
            p.items.ForEach(Shift);
            if (p.spawn != null) Shift(p.spawn);
        }
    }
}
