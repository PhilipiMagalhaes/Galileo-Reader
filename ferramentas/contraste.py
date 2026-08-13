"""Razoes de contraste WCAG 2.2 (1.4.3 / 1.4.6 / 1.4.11) da paleta do MarkReader."""


def canal(v):
    v = v / 255
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def luminancia(hexa):
    h = hexa.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return 0.2126 * canal(r) + 0.7152 * canal(g) + 0.0722 * canal(b)


def razao(a, b):
    la, lb = luminancia(a), luminancia(b)
    claro, escuro = max(la, lb), min(la, lb)
    return (claro + 0.05) / (escuro + 0.05)


CLARO = {
    "fundo":            "#FAFAF9",
    "superficie":       "#F0EFEC",
    "texto":            "#1F2328",
    "texto secundario": "#57606A",
    "borda":            "#8A857C",
    "link":             "#0A58CA",
    "codigo":           "#953800",
    "destaque":         "#FFD54F",
    "destaque texto":   "#1F2328",
    "corrente":         "#C2410C",
    "corrente texto":   "#FFFFFF",
}

ESCURO = {
    "fundo":            "#1E1F22",
    "superficie":       "#2A2C31",
    "texto":            "#E6E6E3",
    "texto secundario": "#A8AEB8",
    "borda":            "#727A86",
    "link":             "#7EB6FF",
    "codigo":           "#FFA657",
    "destaque":         "#7A5800",
    "destaque texto":   "#FFFFFF",
    "corrente":         "#F0B457",
    "corrente texto":   "#1A1400",
}

# (papel, sobre, alvo, tipo)
PARES = [
    ("texto",            "fundo",       7.0, "corpo AAA"),
    ("texto secundario", "fundo",       4.5, "AA"),
    ("link",             "fundo",       4.5, "AA"),
    ("codigo",           "superficie",  4.5, "AA"),
    ("texto",            "superficie",  7.0, "corpo AAA"),
    ("borda",            "fundo",       3.0, "nao-textual 1.4.11"),
    ("borda",            "superficie",  3.0, "nao-textual 1.4.11"),
    ("destaque texto",   "destaque",    4.5, "AA"),
    ("corrente texto",   "corrente",    4.5, "AA"),
    ("corrente",         "destaque",    3.0, "nao-textual 1.4.11"),
]


def tabela(nome, paleta):
    print(f"\n=== {nome}")
    ok = True
    for frente, fundo, alvo, tipo in PARES:
        r = razao(paleta[frente], paleta[fundo])
        passou = r >= alvo
        ok = ok and passou
        print(f"  {frente:<17} sobre {fundo:<12} {r:5.2f}:1  alvo {alvo:.1f}  "
              f"{'OK ' if passou else 'FALHA'}  ({tipo})")
    return ok


tudo = tabela("TEMA CLARO", CLARO)
tudo = tabela("TEMA ESCURO", ESCURO) and tudo
print("\nTODOS OS PARES DENTRO DO ALVO" if tudo else "\nHA PARES FORA DO ALVO")
