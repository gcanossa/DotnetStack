import * as store from "./FileStore.js";

let proxy = null;

async function serializeDropEvent(evt) {
  evt.preventDefault();

  const items = Array.from(evt.dataTransfer.items);

  return {
    files: await Promise.all(
      items.map(async (item) => {
        let file = null;
        if (item.kind === "string") {
          file = await new Promise((resolve, reject) => {
            item.getAsString((text) => {
              let blob = new Blob([text], { type: "text/plain" });
              resolve(new File([blob], "", { type: "text/plain" }));
            });
          });
        } else {
          file = item.getAsFile();
        }

        let fileId = await store.putFile(file);

        return { contentType: file.type, name: file.name, fileId: fileId };
      })
    ),
  };
}

export function connect(dotNetProxy) {
  proxy = dotNetProxy;
  document.addEventListener("drop", drop);
  window.addEventListener("dragover", preventDefault);
}

export function disconnect(dotNetProxy) {
  document.removeEventListener("drop", drop);
  window.removeEventListener("dragover", preventDefault);
  proxy = null;
}

export async function readFile(fileId, consume) {
  let file = await store.getFile(fileId);
  if (file) {
    if (consume) await store.removeFile(fileId);
    return file;
  }

  throw new Error(`File ${fileId} not found.`);
}

export async function removeFile(fileId) {
  await store.removeFile(fileId);
}

async function drop(evt) {
  if (proxy)
    await proxy.invokeMethodAsync("OnDrop", await serializeDropEvent(evt));
}

function preventDefault(evt) {
  evt.preventDefault();
}
