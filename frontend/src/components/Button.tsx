import type { ButtonHTMLAttributes, ReactNode, Ref } from 'react'
import './Button.css'

/** `danger` only for the confirming button of a destructive dialog (delete). */
export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: 'md' | 'sm'
  /** Shows a spinner, keeps the button's width and blocks repeated presses. */
  loading?: boolean
  /** Full width (mobile action bars). */
  block?: boolean
  iconStart?: ReactNode
  iconEnd?: ReactNode
  ref?: Ref<HTMLButtonElement>
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
