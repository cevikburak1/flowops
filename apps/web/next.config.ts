import type { NextConfig } from "next";

const apiOrigin = process.env.FLOWOPS_API_ORIGIN ?? "http://127.0.0.1:5080";

const config: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  experimental: { proxyClientMaxBodySize: 15_000_000 },
  async headers() {
    return [{
      source: "/:path*",
      headers: [
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "X-Frame-Options", value: "DENY" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
      ],
    }];
  },
  async rewrites() {
    return [
      { source: "/auth/:path*", destination: `${apiOrigin}/auth/:path*` },
      { source: "/api/:path*", destination: `${apiOrigin}/api/:path*` },
    ];
  },
};

export default config;
