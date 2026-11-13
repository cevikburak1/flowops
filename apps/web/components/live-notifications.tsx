"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr";

export function LiveNotifications() {
  const router = useRouter();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/api/notifications/hub", HttpTransportType.LongPolling)
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect()
      .build();
    const refresh = () => router.refresh();
    connection.on("NotificationsChanged", refresh);
    connection.onreconnected(refresh);
    void connection.start().catch(() => {
      // The workspace can still fetch stored notifications during ordinary navigation.
    });
    return () => { void connection.stop().catch(() => {}); };
  }, [router]);

  return null;
}
