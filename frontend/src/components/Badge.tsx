import type { ReactNode } from 'react'
import './Badge.css'

export type BadgeTone = 'accent' | 'success' | 'warning' | 'danger' | 'neutral'

interface BadgeProps {
  tone?: BadgeTone
  icon?: ReactNode
  children: ReactNode
}

export function Badge({ tone = 'neutral', icon, children }: BadgeProps) {
  return (
    <span className={`badge badge--${tone}`}>
      {icon}
      {children}
    </span>
  )
}

