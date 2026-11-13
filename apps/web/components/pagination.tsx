import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { messages, type Locale } from "../lib/messages";

export function Pagination({ page, more, href, locale, label }: {
  page: number; more: boolean; href: (page: number) => string; locale: Locale; label: string;
}) {
  if (page === 1 && !more) return null;
  const t = messages[locale];
  return <nav className="list-pagination" aria-label={label}>
    <span aria-current="page">{t.page} {new Intl.NumberFormat(locale).format(page)}</span>
    <div>
      {page > 1 && <Link className="button secondary" href={href(page - 1)}><ChevronLeft size={15} aria-hidden />{t.previous}</Link>}
      {more && <Link className="button secondary" href={href(page + 1)}>{t.next}<ChevronRight size={15} aria-hidden /></Link>}
    </div>
  </nav>;
}
