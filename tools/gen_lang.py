# -*- coding: utf-8 -*-
import sys
sys.path.insert(0, '.')
from cslex import literals, unescape, escape, CY
from translations import T

base = 'C:/Users/Pepsy/work/mrid6-helper/src/'
ru2 = {ru: (en, zh) for ru, en, zh in T}

# 1. все русские литералы интерфейса вне самотеста/логов
s = open(base + 'MainForm.cs', encoding='utf-8').read()
a = s.index('// ------------------------------------------------------------------ самотест')
b = s.index('// ------------------------------------------------------------------ опрос датчиков')
need = []
for (st, en, raw, kind) in literals(s):
    if kind != 'normal' or not CY.search(raw): continue
    if a <= st < b: continue
    line = s[s.rfind('\n', 0, st) + 1: s.find('\n', st)]
    if 'Log.Write(' in line: continue
    k = unescape(raw)
    if k not in need: need.append(k)
missing = [k for k in need if k not in ru2]
if missing:
    print("НЕТ ПЕРЕВОДА:"); [print("  ", repr(m)) for m in missing]; sys.exit(1)
print("покрыто строк интерфейса:", len(need), "из", len(need))

# 2. C#
def cs(d):
    rows = []
    for ru, en, zh in T:
        rows.append('            { "%s", "%s" },' % (escape(ru), escape(d(en, zh))))
    return "\n".join(rows)

code = '''// Автогенерация tools/gen_lang.py — не править вручную. Ключ — русский текст интерфейса.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mrid6Helper
{
    /// <summary>Мультиязычность: ru (исходный), en, zh (упрощённый китайский). Незнакомый ключ возвращается как есть.</summary>
    static class L
    {
        public static string Code = "ru";

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
%s
        };

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
%s
        };

        public static string T(string ru)
        {
            string v;
            if (Code == "en") return En.TryGetValue(ru, out v) ? v : ru;
            if (Code == "zh") return Zh.TryGetValue(ru, out v) ? v : ru;
            return ru;
        }

        /// <summary>Язык Windows → код интерфейса.</summary>
        public static string Detect()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (two == "ru" || two == "uk" || two == "be" || two == "kk") return "ru";
            if (two == "zh") return "zh";
            return "en";
        }

        /// <summary>pref: auto | ru | en | zh</summary>
        public static void Init(string pref)
        {
            Code = (pref == "ru" || pref == "en" || pref == "zh") ? pref : Detect();
        }
    }
}
''' % (cs(lambda en, zh: en), cs(lambda en, zh: zh))
open(base + 'Lang.cs', 'w', encoding='utf-8', newline='\r\n').write(code)
print("Lang.cs записан,", len(T), "записей")
