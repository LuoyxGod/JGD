# 网页原型 → Unity 6 对接文档

> 来源：`E:\unity游戏文件\JGD项目记录\map_editor.html`（地图编辑器）+ `prototype.html`（玩法原型）
> 目的：把已验证的玩法框架移植到 Unity 6（6000.4.11f1，项目 JGDgame）。本文档是三端（网页编辑器 / Blender / Unity）的共同约定。

---

## 一、已实现内容总览

| 模块 | 状态 | 说明 |
|---|---|---|
| 地图编辑器（网页） | ✅ | 画拼图形状、道路（含岔路/断头路）、出口（贴边显示）、出生点、记忆物摆放；参数可调；JSON 导入导出 |
| 3D 俯视角第三人称移动 | ✅ | WASD、相机跟随、分轴滑墙 |
| 区域系统 | ✅ | 初始图层（可含多个散落区块）+ 拼图区域，全部由 JSON 驱动生成 |
| 拼图面板 | ✅ | 拿起/放置/收回、重叠拦截（红字+红块）、相邻校验、确认后世界重建；画布缩放/平移/适应全部 |
| 边缘+障碍双判定碰撞 | ✅ | 区域内畅通；跨区域边界需两侧出口对齐；树/灯笼圆形障碍 |
| 门口实体化 | ✅ | 铁丝网围栏 + 鸟居（原型为程序化几何体，正式版换模型） |
| 生长动画 | ✅ | 地块从地底波浪式升起、树木破土、围栏延迟长出 |
| 记忆物 | ✅ | 初始图层上的=解锁钥匙（按顺序对应拼图）；拼图自带格上的=拼合后出现的额外收集品 |

## 二、JSON 数据契约（三端共同语言）

```json
{
  "version": 2,
  "cfg": { "tileSize": 50, "roadWidth": 8, "doorWidth": 18 },
  "pieces": [
    {
      "name": "初始图层",
      "color": "#c9c2b4",
      "init": true,                      // 初始图层：开局生成、不可收走、全局唯一
      "spawn": [0, 0],                   // 出生点（仅 init 有，格坐标）
      "cells": [[0,0],[0,1]],            // 形状：格坐标，允许散落不连通
      "roads": [ [[-1,0],[-2,0]] ],      // 道路：相邻格对；一端在形状外=断头路；一格拉多条=岔路
      "doors": [ [[-1,0],[-2,0]] ],      // 出口：边界边（格子对），两侧对齐才通行
      "items": [ [1,0] ]                 // 记忆物摆放格
    },
    { "name":"拼图①", "color":"#6f79d6", "init":false, "spawn":null,
      "cells":[[0,0],[0,-1]], "roads":[...], "doors":[...], "items":[...] }
  ]
}
```

**坐标约定（最重要的三条）**
1. 格坐标 (cx, cz) 为整数，世界坐标 = 格坐标 × tileSize；格 (cx,cz) 占据 `[cx*S,(cx+1)*S] × [cz*S,(cz+1)*S]`，中心在 `+S/2`。
2. 每个区域进入世界前做**归一化**：全部数据减去 cells 包围盒左上角 (minX,minZ)——Unity 摆放时按 `包围盒左下角 = 拼图板放置格 × S` 平移，天然对齐。
3. 朝向：游戏内 -z 朝屏幕上方（相机在玩家 +z 侧），地图面板同向。

## 三、网页端系统实现要点（移植时的逻辑参考）

1. **占用与通行**
   - `walkSet`：可站立格集合 = 初始图层全部格 + 已拼合拼图格；
   - 碰撞两层：格判定（四角都在 walkSet 内）+ 边缘判定（跨格查边）+ 障碍判定（树/灯笼圆形半径）；
   - `edgeOpen(A,B)`：同区域内部 → 通；跨区域 → 该边两侧都有出口（doorCount≥2）才通。
2. **doorCount**：`Dictionary<边界中点世界坐标, int>`。区域生成时把自己的出口边 +1；一条边计数=2 → 门开。围栏/鸟居按同一判定决定"门洞"还是"整面网"。
3. **拼图编辑流程**：拿起（placed=null）→ 放置（重叠即时拦截）→ 确认（校验：不重叠 + 至少一边相邻）→ 重建世界；
   - `occupiedSet(手拿拼图)` 与 `validate` 都要排除 `builtPlace[自己]`（自己上一次建成的位置），否则原位重放/移动重叠会被自己挡住；
   - 玩家必须站在初始图层上才能编辑拼图（防把脚下抽掉）。
4. **世界重建**：确认 = 销毁全部动态区域对象 → 按数据重新生成。Unity 对应"销毁 Region 根物体 → 重新实例化 Prefab"，生长动画用 DOTween/Animator。
5. **生长动画**：地块 Y 从 -10 升到位（easeOutCubic），按离区域中心距离延迟；树/灯笼 scale 0→1 破土；围栏延迟 0.35s。
6. **记忆物规则**：初始图层上的按摆放顺序解锁对应顺序的拼图；拼图自带格上的在拼合后出现（额外收集品）；收集进度跨重建保留（按 `区域key:格坐标` 记账）。

## 四、Unity 侧实现映射建议

```
Assets/
  Data/Puzzles/            ← 编辑器导出的 .json（TextAsset）
  Scripts/
    Level/RegionData.cs    ← 一个 piece 的数据：name/color/init/spawn/cells/roads/doors/items
    Level/LevelImporter.cs ← 解析 JSON → List<RegionData>（JsonUtility 或 Newtonsoft）
    Level/GridManager.cs   ← walkSet / cellRegion / doorCount 的 C# 版
    Level/RegionBuilder.cs ← 按数据实例化：地面格 + 道路条 + 围栏/鸟居 + 装饰
    Player/PlayerController.cs ← CharacterController + 分轴尝试移动（复刻 attempt()）
    Puzzle/BoardUI.cs      ← 拼图面板（UGUI/UI Toolkit 拖拽 + 吸附）
  Prefabs/Regions/         ← Blender 每区域整体模型（白盒期用程序化 Cube）
```

- **白盒期**：RegionBuilder 程序化生成（等价网页版），先跑通数据流；
- **换皮期**：每区域一个 Prefab，RegionBuilder 只管"放到哪"，门位缺口由 Blender 按 JSON 数据预留；
- **JSON 为唯一数据来源**，ScriptableObject 只做引用壳，避免两份数据不同步；
- **拼图面板**：网页 Canvas 逻辑可 1:1 翻译成 UI Toolkit `VisualElement` 网格 + Pointer 事件，吸附、`occupiedSet`、`validate` 直接照抄。

## 五、Blender 约定（给建模组员的最简版）

1. 比例：1 单位 = 1 米；地块 = 50m；
2. 每个区域一个导出单元，**原点放在区域包围盒左下角**（与 JSON 归一化原点重合）；
3. 只建模建筑/物件/围栏/鸟居，**门口按 JSON 数据留缺口**（宽度 = doorWidth）；
4. 摆放不用管——Unity 按 JSON 自动放。

## 六、设计决策记录（为什么这样做）

- 对齐靠数据不靠手操：JSON 是唯一权威；
- 拼图无固定答案，只校验"相邻 + 不重叠"：解谜压力来自道路/出口的空间推理；
- 出口通行用"双侧出口计数"，天然支持后续锁钥门扩展；
- 初始图层允许散落区块，但记忆物只刷在出生点连通块：防软锁。

---

*配套文件：`map_editor.html`、`prototype.html`、`原型测试计划书.md`（位于 E:\unity游戏文件\JGD项目记录）*
