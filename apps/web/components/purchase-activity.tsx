"use client";

import Link from "next/link";
import { useState } from "react";
import { messages, type Locale } from "../lib/messages";
import type { Workspace } from "../lib/server";

const activityPeriods = [6, 12, 48] as const;
export type ActivityPeriod = typeof activityPeriods[number];

export function PurchaseActivity({ rows, asOf, period, base, locale }: {
  rows: Workspace["activity"]; asOf: string; period: ActivityPeriod; base: string; locale: Locale;
}) {
  const t = messages[locale];
  const groupSize = period === 48 ? 3 : 1;
  const current = new Date(asOf);
  const monthName = new Intl.DateTimeFormat(locale, { month: "short", timeZone: "UTC" });
  const fullDate = new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" });
  const number = new Intl.NumberFormat(locale);
  const [index, setIndex] = useState<number>();
  const buckets = Array.from({ length: period / groupSize }, (_, i) => {
    const offset = i * groupSize - period + 1;
    const start = new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth() + offset, 1));
    const end = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + groupSize - 1, 1));
    const matches = rows.filter(row => {
      const key = row.year * 12 + row.month - 1;
      const first = start.getUTCFullYear() * 12 + start.getUTCMonth();
      return key >= first && key < first + groupSize;
    });
    const total = matches.reduce((sum, row) => sum + row.count, 0);
    const count = (status: string) => matches.filter(row => row.status === status).reduce((sum, row) => sum + row.count, 0);
    const approved = count("approved");
    const rejected = count("rejected");
    return {
      key: start.toISOString(), total, approved, rejected, open: total - approved - rejected,
      short: groupSize === 3 ? String(start.getUTCFullYear()) : monthName.format(start),
      title: groupSize === 3 ? fullDate.formatRange(start, end) : fullDate.format(start),
    };
  });
  const max = Math.max(1, ...buckets.map(item => item.total));
  const selected = buckets[index ?? buckets.length - 1];
  return <section className="panel activity-chart" aria-labelledby="activity-chart-heading">
    <div className="panel-heading">
      <h2 id="activity-chart-heading">{t.activityChart}</h2>
      <nav className="chart-periods" aria-label={t.activityPeriods}>
        {activityPeriods.map(value => <Link key={value} href={`${base}?months=${value}`}
          aria-current={value === period ? "page" : undefined}>{value} {t.months}</Link>)}
      </nav>
    </div>
    <div className="chart-detail" aria-live="polite">
      <span>{selected.title}</span>
      <strong>{number.format(selected.total)} <span>{t.requestCount}</span></strong>
      <ul className="status-key">
        {([["approved", t.approved, selected.approved], ["review", t.review, selected.open], ["rejected", t.rejected, selected.rejected]] as const)
          .map(([status, name, value]) => <li key={status}><span data-status={status} />{name} <strong>{number.format(value)}</strong></li>)}
      </ul>
    </div>
    <div className="chart-bars" style={{ gridTemplateColumns: `repeat(${buckets.length}, minmax(0, 1fr))` }}>
      {buckets.map((item, i) => <button key={item.key} type="button" className="chart-column"
        aria-label={`${item.title} · ${t.requests} ${number.format(item.total)}`} aria-pressed={item === selected}
        onClick={() => setIndex(i)}>
        <span className="chart-column-track" aria-hidden>
          {([["rejected", item.rejected], ["review", item.open], ["approved", item.approved]] as const)
            .filter(([, value]) => value > 0)
            .map(([status, value]) => <span key={status} data-status={status} style={{ height: `${value / max * 100}%` }} />)}
        </span>
        <span className="chart-tick" aria-hidden>{groupSize === 1 || i % 4 === 0 ? item.short : ""}</span>
      </button>)}
    </div>
  </section>;
}
