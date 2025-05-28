import * as store from "./FileStore.js";

export async function writeText(text) {
  await navigator.clipboard.writeText(text);
}

export async function readText(text) {
  await navigator.clipboard.readText();
}

export async function read() {
  let items = await navigator.clipboard.read();

  return {
    files: await Promise.all(
      items
        .filter((p) => p.types.length > 0)
        .map(async (item) => {
          let type = item.types[0];
          let file = new File([await item.getType(type)], "", { type });

          let fileId = await store.putFile(file);

          return { contentType: file.type, name: file.name, fileId: fileId };
        })
    ),
  };
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
