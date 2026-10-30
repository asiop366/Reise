from pathlib import Path

p = Path(r"C:\Users\aegis\Documents\deepseek-harness\default-workspace\reise-site\docs\index.html")
s = p.read_text(encoding="utf-8")

pairs = [
    # alt text: the mark is a wordmark glyph, the screenshot is the product
    ('<img src="assets/mark.svg" alt="">', '<img src="assets/mark.svg" alt="Reise" width="16" height="16">'),
    # header link needs a real hit area
    (".mark{display:flex;align-items:center;gap:var(--s2)}",
     ".mark{display:flex;align-items:center;gap:var(--s2);min-height:28px}"),
    # facts line: 10px/1 -> readable
    (".facts{display:flex;flex-wrap:wrap;gap:var(--s5);margin-top:var(--s6);\n  font:var(--t1)/1 var(--mono);letter-spacing:.14em;text-transform:uppercase;color:var(--ink-3)}",
     ".facts{display:flex;flex-wrap:wrap;gap:var(--s3) var(--s5);margin-top:var(--s6);\n  font:var(--t2)/1.6 var(--mono);letter-spacing:.12em;text-transform:uppercase;color:var(--ink-3)}"),
    # tags 10px -> 11px, and code blocks must scroll instead of pushing the page wide
    (".tag{font:var(--t1)/1 var(--mono);letter-spacing:.14em;text-transform:uppercase;padding:3px 6px;",
     ".tag{font:var(--t2)/1.2 var(--mono);letter-spacing:.12em;text-transform:uppercase;padding:3px 6px;"),
    (".code pre{margin:0;padding:var(--s4);overflow:auto;font:var(--t3)/1.8 var(--mono);color:var(--ink-2)}",
     ".code{min-width:0}\n.code pre{margin:0;padding:var(--s4);overflow-x:auto;max-width:100%;\n  font:var(--t3)/1.8 var(--mono);color:var(--ink-2)}"),
    (".two{display:grid;grid-template-columns:1fr 1fr;gap:var(--s4)}",
     ".two{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--s4)}"),
    # tables and road maps also need the min-width escape
    ("table{width:100%;border-collapse:collapse;font-size:var(--t4)}",
     "table{width:100%;border-collapse:collapse;font-size:var(--t4);table-layout:fixed}"),
    ("td:first-child{color:var(--ink);width:44%}",
     "td:first-child{color:var(--ink);width:40%}\ntd,th{overflow-wrap:anywhere}"),
]

for old, new in pairs:
    if old not in s:
        print("MISS:", old[:56].replace("\n", " "))
        continue
    s = s.replace(old, new)

p.write_text(s, encoding="utf-8")
print("pages fixes applied")
