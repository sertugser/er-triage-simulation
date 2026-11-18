"""Event graph of the ER model (task #4).

Writes the Graphviz source figures/event_graph.dot and renders
figures/event_graph.png (300 dpi) with the Graphviz ``dot`` program.

    python src/event_graph.py

The .dot file is the editable source: edit it (or this script) and re-render
with ``dot -Kneato -Tpng -Gdpi=300 figures/event_graph.dot -o figures/event_graph.png``.

Notation (Schruben event graph):
  * node   = event; the box under the name lists its state changes
  * edge   = scheduling: when the tail event happens, the head event is
             scheduled after the delay on the edge, if the condition on the
             edge (in parentheses) is true at that moment
  * dashed = delay 0 (the head event happens at the same time)

State variables:
  Q_T  patients waiting for triage      S_N  nurse busy (0/1)
  Q_R  patients waiting for a room      S_R  rooms busy (0..R)
"""

from pathlib import Path
import shutil
import subprocess

FIGURES = Path(__file__).resolve().parent.parent / "figures"

FONT = "Helvetica"
INK = "#1f2933"
MUTED = "#52606d"
EDGE = "#3e4c59"
FILL = "#f5f7fa"
RUN_FILL = "#e4e7eb"
ACCENT = "#0b6e99"


def _node(name: str, title: str, changes: list[str], fill: str = FILL) -> str:
    rows = "".join(
        f'<TR><TD ALIGN="LEFT"><FONT POINT-SIZE="12" COLOR="{MUTED}">{c}</FONT></TD></TR>'
        for c in changes
    )
    label = (
        f'<<TABLE BORDER="0" CELLBORDER="0" CELLSPACING="0" CELLPADDING="3">'
        f'<TR><TD><B><FONT POINT-SIZE="15" COLOR="{INK}">{title}</FONT></B></TD></TR>'
        f'<HR/>{rows}</TABLE>>'
    )
    return (f'  {name} [label={label}, shape=box, style="rounded,filled", '
            f'fillcolor="{fill}", color="{INK}", penwidth=1.4, margin="0.15,0.08"];')


def _edge(tail: str, head: str, delay: str | None = None, cond: str | None = None,
          **attrs) -> str:
    parts = []
    if delay:
        parts.append(f'<FONT COLOR="{ACCENT}"><B>{delay}</B></FONT>')
    if cond:
        parts.append(f'<FONT COLOR="{INK}">({cond})</FONT>')
    label = "<" + "<BR/>".join(parts) + ">" if parts else '""'
    style = attrs.pop("style", "solid" if delay else "dashed")
    extra = "".join(f", {k}={v}" for k, v in attrs.items())
    return (f'  {tail} -> {head} [label={label}, style={style}, color="{EDGE}", '
            f'penwidth=1.3, arrowsize=0.8{extra}];')


# Fixed positions (inches) so the figure keeps the same shape on every render.
# Row 1: run and triage, left to right. Row 2: treatment, right to left.
POSITIONS = {
    "Run": (0.0, 3.2),
    "Arrival": (3.4, 3.2),
    "TriageStart": (7.2, 3.2),
    "TriageEnd": (11.0, 3.2),
    "TreatStart": (11.0, 0.0),
    "TreatEnd": (6.6, 0.0),
    "legend": (1.6, 0.0),
}


def build_dot() -> str:
    nodes = [
        _node("Run", "Run",
              ["Q_T = 0, Q_R = 0", "S_N = 0, S_R = 0"], RUN_FILL),
        _node("Arrival", "Arrival",
              ["Q_T = Q_T + 1", "draw severity"]),
        _node("TriageStart", "Triage Start", ["Q_T = Q_T − 1", "S_N = 1"]),
        _node("TriageEnd", "Triage End", ["S_N = 0", "Q_R = Q_R + 1"]),
        _node("TreatStart", "Treatment Start", ["Q_R = Q_R − 1", "S_R = S_R + 1"]),
        _node("TreatEnd", "Treatment End", ["S_R = S_R − 1"]),
    ]
    edges = [
        _edge("Run", "Arrival", "t<sub>a</sub>"),
        _edge("Arrival", "Arrival", "t<sub>a</sub>", tailport="ne", headport="nw"),
        _edge("Arrival", "TriageStart", None, "S_N = 0", tailport="e", headport="w"),
        _edge("TriageStart", "TriageEnd", "t<sub>triage</sub>", tailport="ne", headport="nw"),
        _edge("TriageEnd", "TriageStart", None, "Q_T &gt; 0", tailport="sw", headport="se"),
        _edge("TriageEnd", "TreatStart", None, "S_R &lt; R"),
        _edge("TreatStart", "TreatEnd", "t<sub>treat</sub>", tailport="sw", headport="se"),
        _edge("TreatEnd", "TreatStart", None, "Q_R &gt; 0", tailport="ne", headport="nw"),
    ]

    def row(text: str, bold: bool = False) -> str:
        color = INK if bold else MUTED
        text = f"<B>{text}</B>" if bold else text
        return f'<TR><TD ALIGN="LEFT"><FONT COLOR="{color}">{text}</FONT></TD></TR>'

    legend = (
        '  legend [shape=plaintext, fontsize=12, label=<'
        '<TABLE BORDER="1" COLOR="#cbd2d9" CELLBORDER="0" CELLSPACING="0" CELLPADDING="3">'
        + row("State variables", True)
        + row("Q_T  patients waiting for triage")
        + row("Q_R  patients waiting for a room")
        + row("S_N  nurse busy (0 or 1)")
        + row("S_R  rooms busy (0 … R), R = 4, 6 or 8")
        + row("Delays", True)
        + row("t<sub>a</sub> ~ Exp(mean 3 or 6 min)")
        + row("t<sub>triage</sub> ~ Tri(min 2, mode 4, max 6)")
        + row("t<sub>treat</sub> ~ N(15, 3) low · N(25, 5) medium · N(45, 8) high")
        + row("severity ~ low .5 · medium .3 · high .2")
        + row("Edges", True)
        + row("solid: schedule after the delay shown")
        + row("dashed: delay 0 (same instant)")
        + row("(…): only if the condition holds when the tail event happens")
        + "</TABLE>>];"
    )
    pos = [f'  {n} [pos="{x},{y}!"];' for n, (x, y) in POSITIONS.items()]
    return "\n".join([
        "// Event graph of the ER triage model. Generated by src/event_graph.py;",
        "// render: dot -Kneato -Tpng -Gdpi=300 figures/event_graph.dot -o figures/event_graph.png",
        "digraph ER_event_graph {",
        f'  graph [layout=neato, splines=curved, fontname="{FONT}", pad=0.4, '
        'bgcolor="white", labelloc=t, fontsize=20, '
        f'label=<<B>ER triage simulation — event graph</B>>, fontcolor="{INK}"];',
        f'  node [fontname="{FONT}"];',
        f'  edge [fontname="{FONT}", fontsize=13];',
        *nodes,
        legend,
        *pos,
        *edges,
        "}",
        "",
    ])


def main() -> None:
    FIGURES.mkdir(exist_ok=True)
    dot_path = FIGURES / "event_graph.dot"
    png_path = FIGURES / "event_graph.png"
    dot_path.write_text(build_dot(), encoding="utf-8")
    print(f"wrote {dot_path}")
    if shutil.which("dot") is None:
        print("Graphviz 'dot' not found; install Graphviz to render the PNG.")
        return
    subprocess.run(["dot", "-Kneato", "-Tpng", "-Gdpi=300", str(dot_path), "-o", str(png_path)], check=True)
    print(f"wrote {png_path}")


if __name__ == "__main__":
    main()
