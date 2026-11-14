import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { CaseDto, CaseStatus, StatsDto } from '../api/types'

export const statusLabels: Record<CaseStatus, string> = {
  New: 'Nowa',
  Sent: 'Wysłana',
  Paid: 'Wypłacona',
  Rejected: 'Odrzucona',
}

export const statusOrder: CaseStatus[] = ['New', 'Sent', 'Paid', 'Rejected']

type Load = { state: 'loading' } | { state: 'error'; message: string } | { state: 'ready'; cases: CaseDto[] }

const messageOf = (error: unknown, fallback: string) => (error instanceof ApiError ? error.detail : fallback)

/**
 * The signed-in user's cases and the summary. A status change shows at once and is rolled back if the server
 * refuses (only that case: other changes made meanwhile stay). The stats are re-read after every change.
 */
export function useCases() {
  const [load, setLoad] = useState<Load>({ state: 'loading' })
  const [stats, setStats] = useState<StatsDto | null>(null)
  const [rowErrors, setRowErrors] = useState<Record<number, string>>({})
  /** Polite announcement of the last change (status changed, case deleted). */
  const [announcement, setAnnouncement] = useState('')
  const active = useRef(true)
  const statsRequest = useRef(0)

  const refreshStats = useCallback(() => {
    // Only the newest request may set the summary: an older one answering last would show stale totals.
    const request = ++statsRequest.current
    api
      .getStats()
      .then((value) => active.current && request === statsRequest.current && setStats(value))
      .catch(() => {
        // The summary is secondary: the list still works without it.
      })
  }, [])

  const fetchAll = useCallback(() => {
    api
      .listCases()
      .then((cases) => active.current && setLoad({ state: 'ready', cases }))
      .catch((error: unknown) => {
        if (active.current) setLoad({ state: 'error', message: messageOf(error, 'Nie udało się wczytać spraw.') })
      })
    refreshStats()
  }, [refreshStats])

  useEffect(() => {
    active.current = true
    fetchAll()
    return () => {
      active.current = false
    }
  }, [fetchAll])

  const reload = () => {
    setLoad({ state: 'loading' })
    fetchAll()
  }

  const patchCase = (id: number, patch: (current: CaseDto) => CaseDto) =>
    setLoad((current) =>
      current.state === 'ready'
        ? { state: 'ready', cases: current.cases.map((item) => (item.id === id ? patch(item) : item)) }
        : current,
    )

  const clearRowError = (id: number) =>
    setRowErrors((current) => {
      if (!(id in current)) return current
      const next = { ...current }
      delete next[id]
      return next
    })

  const changeStatus = async (item: CaseDto, status: CaseStatus) => {
    if (item.status === status) return
    const previous = item.status
    clearRowError(item.id)
    patchCase(item.id, (current) => ({ ...current, status }))
    try {
      const saved = await api.setStatus(item.id, status)
      if (!active.current) return
      // A newer change on this row wins: its own answer applies the server state.
      patchCase(item.id, (current) => (current.status === status ? saved : current))
      setAnnouncement(`Status zmieniony: ${statusLabels[status]}.`)
      refreshStats()
    } catch (error) {
      if (!active.current) return
      // Roll back only if nothing else changed this case in the meantime.
      patchCase(item.id, (current) => (current.status === status ? { ...current, status: previous } : current))
      const reason = messageOf(error, '')
      setRowErrors((current) => ({
        ...current,
        [item.id]: `Nie udało się zmienić statusu – przywrócono „${statusLabels[previous]}”. ${reason}`.trim(),
      }))
    }
  }

  /** Throws ApiError when the server refuses; the caller keeps the dialog open and shows the reason. */
  const remove = async (item: CaseDto) => {
    try {
      await api.deleteCase(item.id)
    } catch (error) {
      // Already gone (deleted elsewhere): the goal is reached.
      if (!(error instanceof ApiError && error.status === 404)) throw error
    }
    if (!active.current) return
    setLoad((current) =>
      current.state === 'ready' ? { state: 'ready', cases: current.cases.filter((c) => c.id !== item.id) } : current,
    )
    clearRowError(item.id)
    setAnnouncement('Sprawa usunięta.')
    refreshStats()
  }

  return { load, stats, rowErrors, announcement, reload, changeStatus, remove }
}
