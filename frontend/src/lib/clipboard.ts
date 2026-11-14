/** Copies text; uses the async Clipboard API and falls back to a hidden textarea (older browsers, non-secure origins). */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // permission denied or not allowed here – try the fallback
  }
  const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
  const area = document.createElement('textarea')
  area.value = text
  area.setAttribute('readonly', '')
  // Off screen, not display:none (it must be selectable); 16 px so iOS does not zoom on focus.
  area.style.position = 'fixed'
  area.style.top = '0'
  area.style.left = '-9999px'
  area.style.fontSize = '16px'
  document.body.append(area)
  area.focus({ preventScroll: true })
  area.select()
  // iOS Safari ignores select() on a read-only field; an explicit range works everywhere.
  area.setSelectionRange(0, text.length)
  try {
    return document.execCommand('copy')
  } catch {
    return false
  } finally {
    area.remove()
    previous?.focus({ preventScroll: true })
  }
}
