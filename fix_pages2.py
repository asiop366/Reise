from pathlib import Path

p = Path(r"C:\Users\aegis\Documents\deepseek-harness\default-workspace\reise-site\docs\index.html")
s = p.read_text(encoding="utf-8")

pairs = [
    # footer links need a 24px+ hit area
    ("footer a:hover{color:var(--ink)}",
     "footer a{display:inline-block;padding:var(--s1) 0;min-height:24px}\nfooter a:hover{color:var(--ink)}"),
    # eyebrow + table heads: 10px/1 -> readable
    (".eyebrow{font:var(--t1)/1 var(--mono);letter-spacing:.28em;",
     ".eyebrow{font:var(--t2)/1.6 var(--mono);letter-spacing:.24em;"),
    ("th{font:500 var(--t1)/1.6 var(--mono);letter-spacing:.18em;text-transform:uppercase;color:var(--ink-3);",
     "th{font:500 var(--t2)/1.6 var(--mono);letter-spacing:.16em;text-transform:uppercase;color:var(--ink-3);"),
]

for old, new in pairs:
    if old not in s:
        print("MISS:", old[:50].replace("\n", " "))
        continue
    s = s.replace(old, new)

p.write_text(s, encoding="utf-8")
print("third pass applied")
