import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

// Gộp class Tailwind, class sau đè class trước khi trùng utility
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
