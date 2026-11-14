/** Client-side checks for ticket files; the same limits as the API (POST /api/extractions). */

export const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/heic', 'image/heif', 'application/pdf'] as const
export const ACCEPT_ATTR = ACCEPTED_TYPES.join(',')
export const MAX_FILES = 4
export const MAX_FILE_BYTES = 5 * 1024 * 1024

const typeByExtension: Record<string, string> = {
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  png: 'image/png',
  webp: 'image/webp',
  heic: 'image/heic',
  heif: 'image/heif',
  pdf: 'application/pdf',
}

const accepted = new Set<string>(ACCEPTED_TYPES)

/** The file's media type, or null when it is not a ticket format. Some browsers report HEIC without a type. */
function resolveType(file: File): string | null {
  const type = file.type.toLowerCase()
  if (accepted.has(type)) return type
  if (type === 'image/jpg') return 'image/jpeg'
  if (type === '' || type === 'application/octet-stream') {
    const extension = file.name.split('.').pop()?.toLowerCase() ?? ''
    return typeByExtension[extension] ?? null
  }
  return null
}

const sameFile = (a: File, b: File) => a.name === b.name && a.size === b.size && a.lastModified === b.lastModified

export interface AddFilesResult {
  files: File[]
  /** Polish, one per problem. */
  errors: string[]
}

/** Adds the valid incoming files to the current ones (max 4 in total); explains every file that was left out. */
export function addTicketFiles(current: readonly File[], incoming: readonly File[]): AddFilesResult {
  const files = [...current]
  const errors: string[] = []
  const skipped: string[] = []

  for (const file of incoming) {
    const type = resolveType(file)
    if (type === null) {
      errors.push(`Format pliku „${file.name}” nie jest obsługiwany. Dodaj zdjęcie (JPG, PNG, WebP, HEIC) lub PDF.`)
    } else if (file.size === 0) {
      errors.push(`Plik „${file.name}” jest pusty.`)
    } else if (file.size > MAX_FILE_BYTES) {
      errors.push(`Plik „${file.name}” jest za duży – maksymalnie 5 MB.`)
    } else if (files.some((existing) => sameFile(existing, file))) {
      // Picked twice: nothing to do.
    } else if (files.length >= MAX_FILES) {
      skipped.push(file.name)
    } else {
      files.push(type === file.type ? file : new File([file], file.name, { type, lastModified: file.lastModified }))
    }
  }

  if (skipped.length > 0) errors.push(`Możesz dodać maksymalnie ${MAX_FILES} pliki – pominięto: ${skipped.join(', ')}.`)
  return { files, errors }
}

export const isPdf = (file: File) => file.type === 'application/pdf'

export function formatFileSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}
