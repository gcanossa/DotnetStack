let proxy = null;

function serializeKeyboardEvent(evt) {
  if (evt) {
    return {
      key: evt.key,
      code: evt.keyCode.toString(),
      location: evt.location,
      repeat: evt.repeat,
      ctrlKey: evt.ctrlKey,
      shiftKey: evt.shiftKey,
      altKey: evt.altKey,
      metaKey: evt.metaKey,
      type: evt.type,
    };
  }
}

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
  if (proxy)
    await proxy.invokeMethodAsync("OnKeyUp", serializeKeyboardEvent(evt));
}
async function keydown(evt) {
  if (proxy)
    await proxy.invokeMethodAsync("OnKeyDown", serializeKeyboardEvent(evt));
}
async function keypress(evt) {
  if (proxy)
    await proxy.invokeMethodAsync("OnKeyPress", serializeKeyboardEvent(evt));
}
