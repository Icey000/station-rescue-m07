using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAgentLab
{
    public enum GameLanguage { Chinese, English }

    public static class LanguageService
    {
        private const string Preference = "StationRescue.Language";
        private static GameLanguage current = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(Preference, 0), 0, 1);
        public static GameLanguage Current => current;
        public static event Action Changed;
        private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
        {
            { "title", new[] { "停电救援", "STATION RESCUE" } },
            { "subtitle", new[] { "M-07 · 最后一班救援船", "M-07 · The last rescue shuttle" } },
            { "story", new[] { "货运站突然停电，维修机器人 M-07 被困舱内。\n找到匹配电芯，启动发电机，为救援船供电后登船撤离。", "A blackout has stranded maintenance droid M-07.\nFind the matching cell, restore emergency power and board the rescue shuttle." } },
            { "start", new[] { "开始救援", "START MISSION" } },
            { "quit", new[] { "退出游戏", "QUIT" } },
            { "pause", new[] { "暂停", "PAUSE" } },
            { "paused", new[] { "任务已暂停", "MISSION PAUSED" } },
            { "resume", new[] { "继续任务", "RESUME" } },
            { "restart", new[] { "新任务", "NEW MISSION" } },
            { "menu", new[] { "返回主菜单", "MAIN MENU" } },
            { "help", new[] { "操作与电芯说明", "CONTROLS & CELLS" } },
            { "close", new[] { "返回", "BACK" } },
            { "step1", new[] { "识别电芯", "MATCH THE CELL" } },
            { "step2", new[] { "接通供电", "RESTORE POWER" } },
            { "step3", new[] { "登船撤离", "EVACUATE" } },
            { "blue", new[] { "蓝色圆柱电芯", "Blue cylindrical cell" } },
            { "orange", new[] { "橙色方盒电芯", "Orange box cell" } },
            { "empty", new[] { "空手", "EMPTY" } },
            { "carry", new[] { "携带物", "CARGO" } },
            { "boost", new[] { "冲刺", "BOOST" } },
            { "ready", new[] { "就绪", "READY" } },
            { "speed", new[] { "速度", "SPEED" } },
            { "goal.fetch", new[] { "取来 {0}", "Fetch the {0}" } },
            { "goal.deliver", new[] { "将电芯送到应急发电机", "Deliver the cell to the generator" } },
            { "goal.wrong", new[] { "型号不符：归还后换成 {0}", "Wrong type: return it and fetch the {0}" } },
            { "goal.powering", new[] { "供电正在接通 · 沿线路观察港口", "Restoring power · watch the dock circuit" } },
            { "goal.board", new[] { "前往 {0}，交互登船", "Go to {0} and interact to board" } },
            { "goal.complete", new[] { "M-07 已安全撤离", "M-07 evacuated safely" } },
            { "dock.a", new[] { "A · 北侧港", "A · North dock" } },
            { "dock.b", new[] { "B · 东侧港", "B · East dock" } },
            { "dock.c", new[] { "C · 西侧港", "C · West dock" } },
            { "generator", new[] { "应急发电机", "Emergency generator" } },
            { "reclaim", new[] { "电芯归还柜", "Cell return cabinet" } },
            { "pickup", new[] { "拾取电芯", "PICK UP CELL" } },
            { "return", new[] { "归还电芯", "RETURN CELL" } },
            { "install", new[] { "安装电芯", "INSERT CELL" } },
            { "board", new[] { "登船撤离", "BOARD SHUTTLE" } },
            { "need", new[] { "需要 {0}", "Requires a {0}" } },
            { "mismatch", new[] { "型号不匹配", "CELL TYPE MISMATCH" } },
            { "offline", new[] { "断电 · 先修复发电机", "OFFLINE · restore the generator" } },
            { "standby", new[] { "备用港口 · 供电不足", "STANDBY · insufficient reserve power" } },
            { "connecting", new[] { "正在接通供电", "POWER CONNECTION IN PROGRESS" } },
            { "wait.arc", new[] { "电缆正在放电 · 等待停歇", "LIVE CABLE · wait for the safe window" } },
            { "stunned", new[] { "磁吸保护器复位中", "MAGNET PROTECTION RESETTING" } },
            { "picked", new[] { "已吸附 {0}", "Picked up the {0}" } },
            { "returned", new[] { "电芯正在入柜并返回充电架", "Returning the cell through the cabinet" } },
            { "denied", new[] { "型号不匹配，电芯保留（+3秒）", "Wrong cell type; cargo retained (+3s)" } },
            { "shock", new[] { "触电失速！电芯掉落，停歇后可捡回（+5秒）", "Electrical shock! Retrieve dropped cargo when safe (+5s)" } },
            { "shock.empty", new[] { "触电失速！等待或绕开电缆（+5秒）", "Electrical shock! Wait or take a detour (+5s)" } },
            { "power.start", new[] { "电芯接入 · 漏电线路已隔离", "Cell inserted · damaged circuit isolated" } },
            { "power.ready", new[] { "{0} 已通电，靠近后交互登船", "{0} is ready. Approach and interact to board" } },
            { "won", new[] { "救援任务完成", "MISSION COMPLETE" } },
            { "score", new[] { "任务用时 {0} 秒 · 错误加时 {1} 秒", "Mission time {0}s · penalties {1}s" } },
            { "intro.controls", new[] { "WASD 移动 · E 交互 · Shift 冲刺 · Esc 暂停", "WASD move · E interact · Shift boost · Esc pause" } },
            { "help.body", new[] {
                "移动：WASD 或方向键。头部会朝移动方向转动。\n\n交互：靠近后按 E，或点击动作按钮。拾取、归还、安装和登船都需要交互；一次携带一块电芯。\n\n冲刺：按一下 Shift，冷却 1.1 秒。普通速度 6，冲刺速度上限 11.5。蓝色尾焰表示推进器启动。\n\n电芯：观察发电机插槽，匹配蓝色圆柱或橙色方盒。拿错可送到归还柜；错误安装加时 3 秒。\n\n电缆：放电 2 秒，停歇 2.5 秒。触电会短暂停顿、掉落电芯并加时 5 秒；等电弧消失再捡回。\n\n撤离：沿亮起的绿色线路，前往本局唯一供电的港口，交互登船。\n\nEsc 暂停；R 开始新任务。55 秒内三星，90 秒内两星。",
                "MOVE: WASD or arrow keys. The head turns toward movement.\n\nINTERACT: Press E nearby or click the action button. Picking up, returning, installing and boarding all require an action. Carry one cell at a time.\n\nBOOST: Tap Shift. Cooldown: 1.1s. Normal speed: 6; boost speed limit: 11.5. Blue exhaust indicates thrust.\n\nCELLS: Match the generator socket: blue cylinder or orange box. Return unwanted cargo at the cabinet. A wrong installation adds 3s.\n\nCABLES: Live for 2s, safe for 2.5s. A shock briefly stalls the droid, drops cargo and adds 5s. Retrieve it when the arcs stop.\n\nEVACUATE: Follow the green circuit to the single powered dock and interact to board.\n\nEsc pauses; R starts a new mission. Three stars within 55s, two within 90s."
            } },
            { "credits", new[] { "空间站素材：Kenney（CC0） · M-07 与电芯：本项目制作", "Station assets: Kenney (CC0) · M-07 and cells: project originals" } }
        };
        public static void Set(GameLanguage language)
        {
            if (current == language) return;
            current = language;
            PlayerPrefs.SetInt(Preference, (int)language); PlayerPrefs.Save();
            Changed?.Invoke();
        }
        public static string Text(string key, params object[] args)
        {
            string value = Strings.TryGetValue(key, out string[] pair) ? pair[(int)current] : key;
            return args.Length == 0 ? value : string.Format(value, args);
        }
        public static string Cell(ModuleKind kind) => Text(kind == ModuleKind.BlueCircle ? "blue" : "orange");
    }
}
