export async function downloadFileFromStream(fileName, contentStreamReference) {
  const url = await createObjectURLFromStream(contentStreamReference);
  const anchorElement = document.createElement("a");
  anchorElement.href = url;
  anchorElement.download = fileName ?? "";
  anchorElement.click();
  anchorElement.remove();
  await revokeObjectURL(url);
}

export async function createObjectURLFromStream(
  contentStreamReference,
  contentType
) {
  const arrayBuffer = await contentStreamReference.arrayBuffer();
  const blob = new Blob([arrayBuffer], { type: contentType });
  const url = URL.createObjectURL(blob);

  return url;
}

export async function revokeObjectURL(url) {
  URL.revokeObjectURL(url);
}
