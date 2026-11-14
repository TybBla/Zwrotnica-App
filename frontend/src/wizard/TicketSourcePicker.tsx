import { useEffect, useRef, useState, type ChangeEvent, type DragEvent } from 'react'
import { Button } from '../components/Button'
import { AlertCircleIcon, CameraIcon, CloseIcon, FileIcon, ImageIcon, UploadIcon } from '../components/icons'
import { useMediaQuery } from '../hooks/useMediaQuery'
import { ACCEPT_ATTR, MAX_FILES, addTicketFiles, formatFileSize, isPdf } from './ticketFiles'
import './TicketSourcePicker.css'

interface TicketSourcePickerProps {
  files: File[]
  onFilesChange: (files: File[]) => void
  disabled?: boolean
}

/**
 * Where the ticket comes from. Touch devices (coarse pointer): "Zrób zdjęcie" (rear camera) and
 * "Wybierz z galerii lub pliku". Mouse: a drop zone with "Wybierz plik". 1–4 files, previewed locally.
 */
export function TicketSourcePicker({ files, onFilesChange, disabled = false }: TicketSourcePickerProps) {
  const touch = useMediaQuery('(pointer: coarse)')
  const [errors, setErrors] = useState<string[]>([])
  const [dragging, setDragging] = useState(false)
  const cameraRef = useRef<HTMLInputElement>(null)
  const pickerRef = useRef<HTMLInputElement>(null)
  const full = files.length >= MAX_FILES
  const blocked = disabled || full

  const add = (incoming: File[]) => {
    if (incoming.length === 0) return
    const result = addTicketFiles(files, incoming)
    setErrors(result.errors)
    if (result.files.length !== files.length) onFilesChange(result.files)
  }

  const onInput = (event: ChangeEvent<HTMLInputElement>) => {
    add([...(event.target.files ?? [])])
    event.target.value = '' // the same file can be picked again after removing it
  }

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    if (!blocked) add([...event.dataTransfer.files])
  }

  const remove = (file: File) => {
    setErrors([])
    onFilesChange(files.filter((existing) => existing !== file))
  }

  const fileInput = (ref: typeof pickerRef, camera: boolean) => (
    <input
      ref={ref}
      type="file"
      accept={ACCEPT_ATTR}
      multiple={!camera}
      {...(camera ? { capture: 'environment' as const } : {})}
      className="visually-hidden"
      tabIndex={-1}
      aria-hidden="true"
      disabled={blocked}
      onChange={onInput}
    />
  )

  return (
    <div className="source">
      {touch ? (
        <div className="source__buttons">
          {/* Once a file is in, "Odczytaj dane" becomes the one primary action on screen. */}
          <Button
            block
            variant={files.length > 0 ? 'secondary' : 'primary'}
            iconStart={<CameraIcon />}
            disabled={blocked}
            onClick={() => cameraRef.current?.click()}
          >
            {files.length > 0 ? 'Zrób zdjęcie kolejnej strony' : 'Zrób zdjęcie'}
          </Button>
          <Button block variant="secondary" iconStart={<ImageIcon />} disabled={blocked} onClick={() => pickerRef.current?.click()}>
            Wybierz z galerii lub pliku
          </Button>
          {fileInput(cameraRef, true)}
          {fileInput(pickerRef, false)}
        </div>
      ) : (
        <div
          className="source__drop"
          data-dragging={dragging || undefined}
          data-disabled={blocked || undefined}
          onDragEnter={(event) => {
            event.preventDefault()
            if (!blocked) setDragging(true)
          }}
          onDragOver={(event) => {
            event.preventDefault()
            event.dataTransfer.dropEffect = blocked ? 'none' : 'copy'
          }}
          onDragLeave={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragging(false)
          }}
          onDrop={onDrop}
        >
          <span className="source__drop-icon" aria-hidden="true">
            <UploadIcon size={22} />
          </span>
          <p className="source__drop-title">Przeciągnij tutaj zdjęcie lub PDF biletu</p>
          <p className="source__drop-or">albo</p>
          <Button variant="secondary" size="sm" disabled={blocked} onClick={() => pickerRef.current?.click()}>
            Wybierz plik
          </Button>
          {fileInput(pickerRef, false)}
        </div>
      )}

      <p className="source__limits">
        {full
          ? `Dodano ${MAX_FILES} pliki – to maksimum. Usuń któryś, aby dodać inny.`
          : 'JPG, PNG, WebP, HEIC lub PDF · do 4 plików (np. kilka stron biletu) · maks. 5 MB każdy'}
      </p>

      {errors.length > 0 && (
        <div className="source__errors" role="alert">
          {errors.map((error) => (
            <p key={error}>
              <AlertCircleIcon size={16} />
              {error}
            </p>
          ))}
        </div>
      )}

      {files.length > 0 && (
        <ul className="source__files" aria-label="Dodane pliki">
          {files.map((file) => (
            <FilePreview key={`${file.name}-${file.size}-${file.lastModified}`} file={file} disabled={disabled} onRemove={() => remove(file)} />
          ))}
        </ul>
      )}
    </div>
  )
}

function FilePreview({ file, disabled, onRemove }: { file: File; disabled: boolean; onRemove: () => void }) {
  const [broken, setBroken] = useState(false)
  const imageRef = useRef<HTMLImageElement>(null)
  const pdf = isPdf(file)
  const canPreview = !pdf && typeof URL.createObjectURL === 'function'

  // Local preview only: the object URL lives as long as the thumbnail and never leaves the browser.
  useEffect(() => {
    const image = imageRef.current
    if (!image) return
    const objectUrl = URL.createObjectURL(file)
    image.src = objectUrl
    return () => URL.revokeObjectURL(objectUrl)
  }, [file])

  return (
    <li className="source__file">
      <span className="source__thumb" aria-hidden="true">
        {pdf ? <FileIcon size={22} /> : <ImageIcon size={22} />}
        {/* HEIC does not render in most desktop browsers: the icon underneath stays visible. */}
        {canPreview && !broken && <img ref={imageRef} alt="" onError={() => setBroken(true)} />}
      </span>
      <span className="source__file-text">
        <span className="source__file-name">{file.name}</span>
        <span className="source__file-meta">
          {pdf ? 'PDF' : 'Zdjęcie'} · {formatFileSize(file.size)}
        </span>
      </span>
      <button type="button" className="source__remove" onClick={onRemove} disabled={disabled} aria-label={`Usuń plik ${file.name}`}>
        <CloseIcon size={18} />
      </button>
    </li>
  )
}
