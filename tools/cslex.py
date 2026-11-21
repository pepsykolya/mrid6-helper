import re

CY = re.compile('[\u0400-\u04FF]')

def literals(src):
    """Вернуть список (start, end, raw, kind) обычных строковых литералов C# вне комментариев/символьных литералов."""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        nx = src[i + 1] if i + 1 < n else ''
        if c == '/' and nx == '/':
            j = src.find('\n', i)
            i = n if j < 0 else j
        elif c == '/' and nx == '*':
            j = src.find('*/', i + 2)
            i = n if j < 0 else j + 2
        elif c == '@' and nx == '"':
            j = i + 2
            while j < n:
                if src[j] == '"':
                    if j + 1 < n and src[j + 1] == '"': j += 2; continue
                    break
                j += 1
            out.append((i, j + 1, src[i + 2:j], 'verbatim')); i = j + 1
        elif c == '$' and nx == '"':
            j = i + 2
            while j < n and src[j] != '"':
                j += 2 if src[j] == '\\' else 1
            out.append((i, j + 1, src[i + 2:j], 'interp')); i = j + 1
        elif c == '"':
            j = i + 1
            while j < n and src[j] != '"':
                j += 2 if src[j] == '\\' else 1
            out.append((i, j + 1, src[i + 1:j], 'normal')); i = j + 1
        elif c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == '\\' else 1
            i = j + 1
        else:
            i += 1
    return out

def unescape(raw):
    return (raw.replace('\\"', '"').replace('\\n', '\n').replace('\\r', '\r').replace('\\t', '\t').replace('\\\\', '\\'))

def escape(s):
    return s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\r', '\\r').replace('\t', '\\t')

def ui_literals(src, skip_ranges):
    res = []
    for (a, b, raw, kind) in literals(src):
        if kind != 'normal' or not CY.search(raw): continue
        if any(lo <= a < hi for lo, hi in skip_ranges): continue
        res.append((a, b, raw))
    return res
