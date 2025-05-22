const fileStore = {};

async function getSha256(data) {
  let buffer;
  if (typeof data === "string") buffer = new TextEncoder().encode(data);
  else if (data instanceof Uint8Array) buffer = data;
  else buffer = new Uint8Array(data);

  const hashBuffer = await window.crypto.subtle.digest("SHA-256", buffer);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  const hashHex = hashArray
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
  return hashHex;
}

export async function getFile(fileId) {
  return fileStore[fileId] ?? null;
}

export async function putFile(file) {
  let data = new Uint8Array(await file.arrayBuffer());

  let fileId =
    Math.round(Math.random() * 1000) + ":" + (await getSha256(data.buffer));
  fileStore[fileId] = data;

  return fileId;
}

export async function removeFile(fileId) {
  delete fileStore[fileId];
}
