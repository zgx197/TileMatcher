import { bindActions, initializeWorkbench } from "./modules/actions.js";
import { getDomRefs } from "./modules/dom.js";
import { renderApp } from "./modules/renderers.js";
import { createAppState } from "./modules/state.js";

const dom = getDomRefs();
const state = createAppState();

renderApp(dom, state);
bindActions(dom, state);
initializeWorkbench(dom, state);
