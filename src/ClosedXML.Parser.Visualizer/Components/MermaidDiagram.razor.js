// Draws a Mermaid diagram for MermaidDiagram.razor.
//
// Mermaid is loaded from jsdelivr when the first diagram is drawn. The version is pinned here.
// Dependabot does not read this file, so update the version by hand.
const MERMAID_URL = "https://cdn.jsdelivr.net/npm/mermaid@11.17.2/dist/mermaid.esm.min.mjs";

let mermaidPromise = null;
let renderCount = 0;
const latestRender = new WeakMap();

function loadMermaid() {
    mermaidPromise ??= import(MERMAID_URL)
        .then(({ default: mermaid }) => {
            mermaid.initialize({
                startOnLoad: false,
                securityLevel: "strict",
                // Throw on a bad definition, instead of drawing Mermaid's error diagram.
                suppressErrorRendering: true,
                theme: "neutral",
                // Draw the diagram at its natural size. The page scrolls a wide tree sideways.
                flowchart: { useMaxWidth: false, nodeSpacing: 24, rankSpacing: 40 },
            });
            return mermaid;
        })
        .catch(error => {
            // Let the next render try again, for example after the network is back.
            mermaidPromise = null;
            throw error;
        });
    return mermaidPromise;
}

export function attach(host, dotNet) {
    host.addEventListener("click", event => {
        const node = event.target.closest("g.node");
        dotNet.invokeMethodAsync("NodeClicked", node ? nodeIdOf(node) : null);
    });
}

export async function render(host, definition) {
    const id = ++renderCount;
    latestRender.set(host, id);
    try {
        const mermaid = await loadMermaid();
        const { svg } = await mermaid.render(`mermaid-${id}`, definition);
        if (latestRender.get(host) !== id) {
            return { stale: true, error: null };
        }

        // Replace the old diagram in one step, so the page does not flicker.
        host.innerHTML = svg;
        return { stale: false, error: null };
    } catch (error) {
        if (latestRender.get(host) !== id) {
            return { stale: true, error: null };
        }

        return { stale: false, error: error?.message ?? String(error) };
    }
}

export function select(host, nodeId) {
    for (const node of host.querySelectorAll("g.node")) {
        node.classList.toggle("selected", nodeId !== null && nodeIdOf(node) === nodeId);
    }
}

// The node ids of the diagram are n0, n1, ... Mermaid puts the id into the element id,
// for example "mermaid-3-flowchart-n12-0", and in newer versions also into data-id.
function nodeIdOf(element) {
    if (element.dataset.id) {
        return element.dataset.id;
    }

    const match = /(?:^|-)(n\d+)(?:-|$)/.exec(element.id);
    return match ? match[1] : null;
}
