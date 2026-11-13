import type { HTMLAttributes } from 'react'
import './Card.css'

interface CardProps extends HTMLAttributes<HTMLElement> {
  as?: 'div' | 'section' | 'article'
  padding?: 'md' | 'lg'
  /** Raised cards float above the canvas (results, menus); flat ones group content. */
  elevation?: 'flat' | 'raised'
}

export function Card({ as: Element = 'div', padding = 'md', elevation = 'flat', className, ...props }: CardProps) {
  const classes = ['card', `card--pad-${padding}`, `card--${elevation}`, className].filter(Boolean).join(' ')
  return <Element {...props} className={classes} />
}
