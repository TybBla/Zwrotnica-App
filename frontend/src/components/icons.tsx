import {
  ArrowLeft,
  ArrowRight,
  Camera,
  Check,
  ChevronDown,
  CircleAlert,
  CircleCheck,
  CircleMinus,
  Clock,
  ExternalLink,
  FileText,
  Image,
  Info,
  LogOut,
  Moon,
  Pencil,
  ScanText,
  ShieldCheck,
  Sun,
  Ticket,
  TriangleAlert,
  Upload,
  User,
  X,
  type LucideIcon,
  type LucideProps,
} from 'lucide-react'

/**
 * The app's icon set: lucide-react with one stroke and size everywhere (20 px, 1.75 stroke), decorative by default.
 * A thin re-export so call sites stay `<InfoIcon size={16} />` and the library can change in one place.
 */
export type IconProps = LucideProps

function icon(Glyph: LucideIcon) {
  function AppIcon(props: IconProps) {
    return <Glyph size={20} strokeWidth={1.75} aria-hidden="true" focusable="false" {...props} />
  }
  AppIcon.displayName = `${Glyph.displayName ?? 'Lucide'}Icon`
  return AppIcon
}

export const InfoIcon = icon(Info)
export const SunIcon = icon(Sun)
export const MoonIcon = icon(Moon)
export const ChevronDownIcon = icon(ChevronDown)
export const ArrowRightIcon = icon(ArrowRight)
export const ArrowLeftIcon = icon(ArrowLeft)
export const UserIcon = icon(User)
export const LogOutIcon = icon(LogOut)
export const CheckCircleIcon = icon(CircleCheck)
export const MinusCircleIcon = icon(CircleMinus)
export const AlertCircleIcon = icon(CircleAlert)
export const WarningIcon = icon(TriangleAlert)
export const TicketIcon = icon(Ticket)
export const ClockIcon = icon(Clock)
export const ShieldIcon = icon(ShieldCheck)
export const CheckIcon = icon(Check)
export const CloseIcon = icon(X)
export const CameraIcon = icon(Camera)
export const ImageIcon = icon(Image)
export const FileIcon = icon(FileText)
export const UploadIcon = icon(Upload)
export const PencilIcon = icon(Pencil)
/** Reading the ticket (AI extraction): a document being scanned, not a "magic" sparkle. */
export const ScanIcon = icon(ScanText)
export const ExternalLinkIcon = icon(ExternalLink)
