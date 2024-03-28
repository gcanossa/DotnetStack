let proxy = null;

export function connect(dotNetProxy) {
  proxy = dotNetProxy;
  document.addEventListener("keyup", keyup);
  document.addEventListener("keydown", keydown);
  document.addEventListener("keypress", keypress);
}

export function disconnect(dotNetProxy) {
  document.removeEventListener("keyup", keyup);
  document.removeEventListener("keydown", keydown);
  document.removeEventListener("keypress", keypress);
  proxy = null;
}

async function keyup(evt) {
  if (proxy) await proxy.invokeMethodAsync("OnKeyUp", evt);
}
async function keydown(evt) {
  if (proxy) await proxy.invokeMethodAsync("OnKeyDown", cardId);
}
async function keypress(evt) {
  if (proxy) await proxy.invokeMethodAsync("OnKeyPress", cardId);
}
