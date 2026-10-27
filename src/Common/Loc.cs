using System.Collections.Generic;
using System.Linq;

// One row per string holding both languages, so a translation can never be missing for just one of them.
static class Loc
{
    static readonly string[][] Entries =
    {
        // key, English, Thai
        new[] { "common.ok", "OK", "ตกลง" },
        new[] { "common.cancel", "Cancel", "ยกเลิก" },

        new[] { "action.Region", "Capture Selected Area", "จับภาพพื้นที่ที่เลือก" },
        new[] { "action.FullScreen", "Capture Entire Screen", "จับภาพทั้งหน้าจอ" },
        new[] { "action.Window", "Capture Window", "จับภาพหน้าต่าง" },
        new[] { "action.RepeatLastRegion", "Repeat Last Selection", "จับพื้นที่เดิมซ้ำ" },
        new[] { "action.Ocr", "Copy Text from Screen (OCR)", "คัดลอกข้อความจากหน้าจอ (OCR)" },
        new[] { "action.Record", "Record Screen", "บันทึกหน้าจอ" },
        new[] { "action.StopRecording", "Stop Recording", "หยุดบันทึก" },

        new[] { "menu.timer", "Timer", "ตั้งเวลาหน่วง" },
        new[] { "menu.timer.off", "Off", "ปิด" },
        new[] { "menu.timer.seconds", "{0} seconds", "{0} วินาที" },
        new[] { "menu.openFolder", "Open Screenshots Folder", "เปิดโฟลเดอร์รูปภาพ" },
        new[] { "menu.settings", "Settings…", "ตั้งค่า…" },
        new[] { "menu.exit", "Exit", "ออก" },

        new[] { "tray.recording", "Recording… press {0} to stop", "กำลังบันทึก… กด {0} เพื่อหยุด" },
        new[] { "notify.error", "Something went wrong", "เกิดข้อผิดพลาด" },
        new[] { "notify.hotkeyConflict", "Another program already uses: {0}. Change them in Settings.", "โปรแกรมอื่นใช้ปุ่มลัดนี้อยู่: {0} เปลี่ยนได้ในหน้าตั้งค่า" },
        new[] { "recording.failed", "Recording failed", "บันทึกหน้าจอไม่สำเร็จ" },

        new[] { "countdown", "Capturing in {0}…", "จะจับภาพใน {0}…" },

        new[] { "selector.hint.region", "Drag to select  ·  Space: window  ·  Esc: cancel", "ลากเพื่อเลือกพื้นที่  ·  Space: หน้าต่าง  ·  Esc: ยกเลิก" },
        new[] { "selector.hint.copyColor", "C: copy color", "C: คัดลอกสี" },
        new[] { "selector.hint.window", "Click a window  ·  Space: area  ·  Esc: cancel", "คลิกที่หน้าต่าง  ·  Space: พื้นที่  ·  Esc: ยกเลิก" },
        new[] { "selector.colorCopied", "Copied {0}", "คัดลอกแล้ว {0}" },

        new[] { "ocr.noEngine", "Windows has no OCR language installed.", "Windows ยังไม่ได้ติดตั้งภาษาสำหรับ OCR" },
        new[] { "ocr.copied", "Text copied", "คัดลอกข้อความแล้ว" },
        new[] { "ocr.nothing", "No text found in the selection.", "ไม่พบข้อความในพื้นที่ที่เลือก" },

        new[] { "thumb.copy", "Copy", "คัดลอก" },
        new[] { "thumb.open", "Open", "เปิด" },
        new[] { "thumb.edit", "Markup…", "แก้ไข (Markup)…" },
        new[] { "thumb.showInFolder", "Show in Folder", "แสดงในโฟลเดอร์" },
        new[] { "thumb.saveAs", "Save As…", "บันทึกเป็น…" },
        new[] { "thumb.delete", "Move to Recycle Bin", "ย้ายไปถังขยะ" },
        new[] { "thumb.dismiss", "Dismiss", "ปิด" },

        new[] { "markup.title", "Markup — {0}", "แก้ไขรูป — {0}" },
        new[] { "markup.tool.Pen", "Pen", "ปากกา" },
        new[] { "markup.tool.Arrow", "Arrow", "ลูกศร" },
        new[] { "markup.tool.Rectangle", "Rectangle", "สี่เหลี่ยม" },
        new[] { "markup.tool.Ellipse", "Ellipse", "วงรี" },
        new[] { "markup.tool.Text", "Text", "ข้อความ" },
        new[] { "markup.tool.Blur", "Blur", "เบลอ" },
        new[] { "markup.color", "Color", "สี" },
        new[] { "markup.thickness", "Thickness", "ความหนา" },
        new[] { "markup.undo", "Undo", "ย้อนกลับ" },
        new[] { "markup.save", "Save", "บันทึก" },
        new[] { "markup.saveCopy", "Save a Copy…", "บันทึกสำเนา…" },
        new[] { "markup.copy", "Copy", "คัดลอก" },
        new[] { "markup.discard", "Discard your changes?", "ทิ้งการแก้ไขที่ยังไม่ได้บันทึกหรือไม่?" },
        new[] { "markup.textPrompt", "Text", "ข้อความ" },

        new[] { "settings.title", "Settings", "ตั้งค่า" },
        new[] { "settings.tab.general", "General", "ทั่วไป" },
        new[] { "settings.tab.capture", "Capture", "การจับภาพ" },
        new[] { "settings.tab.hotkeys", "Hotkeys", "ปุ่มลัด" },
        new[] { "settings.tab.recording", "Recording", "บันทึกหน้าจอ" },
        new[] { "settings.saveFolder", "Save screenshots to", "บันทึกรูปไปที่" },
        new[] { "settings.browse", "Browse…", "เลือก…" },
        new[] { "settings.browseDescription", "Choose where screenshots are saved", "เลือกโฟลเดอร์สำหรับเก็บรูป" },
        new[] { "settings.fileName", "File name", "ชื่อไฟล์" },
        new[] { "settings.fileNameHint", "Use {date} and {time}", "ใช้ {date} และ {time} ได้" },
        new[] { "settings.preview", "Example", "ตัวอย่าง" },
        new[] { "settings.format", "Image format", "รูปแบบไฟล์" },
        new[] { "settings.jpegQuality", "JPEG quality", "คุณภาพ JPEG" },
        new[] { "settings.language", "Language", "ภาษา" },
        new[] { "settings.startup", "Start when Windows starts", "เปิดพร้อม Windows" },
        new[] { "settings.includeCursor", "Include the mouse pointer", "รวมเคอร์เซอร์เมาส์ในภาพ" },
        new[] { "settings.showMagnifier", "Show a magnifier with the pixel colour while selecting (C copies the colour)", "แสดงแว่นขยายและค่าสีที่เมาส์ตอนเลือกพื้นที่ (กด C คัดลอกสี)" },
        new[] { "settings.shutter", "Play shutter sound", "เล่นเสียงชัตเตอร์" },
        new[] { "settings.clipboard", "Also copy to clipboard", "คัดลอกไปยัง clipboard ด้วย" },
        new[] { "settings.timer", "Timer delay", "หน่วงเวลาก่อนจับภาพ" },
        new[] { "settings.hotkeysHint", "Click a box, then press the new shortcut (it needs Ctrl, Alt or Shift).", "คลิกที่ช่อง แล้วกดปุ่มลัดใหม่ (ต้องมี Ctrl, Alt หรือ Shift ร่วมด้วย)" },
        new[] { "settings.hotkeysReset", "Reset to defaults", "คืนค่าเริ่มต้น" },
        new[] { "settings.recordFormat", "Recording format", "รูปแบบการบันทึก" },
        new[] { "settings.recordFps", "Frames per second", "เฟรมต่อวินาที" },
        new[] { "settings.recordMaxSeconds", "Maximum length (seconds)", "ความยาวสูงสุด (วินาที)" },
        new[] { "settings.ffmpegMissing", "MP4 needs ffmpeg.exe on your PATH.", "MP4 ต้องมี ffmpeg.exe ใน PATH" },
        new[] { "settings.error.folder", "The save folder can't be used: {0}", "ใช้โฟลเดอร์นี้ไม่ได้: {0}" },
        new[] { "settings.error.template", "Enter a file name.", "กรุณากรอกชื่อไฟล์" },
        new[] { "settings.error.duplicateHotkey", "{0} is assigned to more than one action.", "{0} ถูกกำหนดให้มากกว่าหนึ่งคำสั่ง" }
    };

    static readonly Dictionary<string, string[]> EntriesByKey = Entries.ToDictionary(entry => entry[0]);

    public static string Language { get; set; }

    public static IEnumerable<string> Keys { get { return EntriesByKey.Keys; } }

    // An unknown key is returned as-is so a typo is visible on screen instead of crashing.
    public static string T(string key)
    {
        string[] entry;
        if (!EntriesByKey.TryGetValue(key, out entry)) return key;
        return Language == "th" ? entry[2] : entry[1];
    }
}
