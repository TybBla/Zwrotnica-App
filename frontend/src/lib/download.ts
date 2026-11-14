/** Saves a blob under `fileName` via a temporary object URL, revoked right after the download starts. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.rel = 'noopener'
  document.body.append(link)
  link.click()
  link.remove()
  // Some browsers read the URL asynchronously after click(); give them a moment before revoking it.
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
