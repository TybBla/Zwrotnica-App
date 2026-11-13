import type { ButtonHTMLAttributes, ReactNode } from 'react'
import './Button.css'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: 'md' | 'sm'
  /** Shows a spinner, keeps the button's width and blocks repeated presses. */
  loading?: boolean
  /** Full width (mobile action bars). */
  block?: boolean
  iconStart?: ReactNode
  iconEnd?: ReactNode
}

export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  block = false,
  iconStart,
  iconEnd,
  className,
  disabled,
  children,
  type = 'button',
  ...props
}: ButtonProps) {
  const classes = ['button', `button--${variant}`, `button--${size}`, block && 'button--block', className]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      {...props}
      type={type}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      data-loading={loading || undefined}
    >
      <span className="button__content">
        {iconStart}
        {children}
        {iconEnd}
      </span>
      {loading && <span className="button__spinner" aria-hidden="true" />}
    </button>
  )
}
