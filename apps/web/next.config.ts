import type { NextConfig } from "next";

const apiOrigin = process.env.FLOWOPS_API_ORIGIN ?? "http://127.0.0.1:5080";

const config: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  async rewrites() {
    return [
      { source: "/auth/:path*", destination: `${apiOrigin}/auth/:path*` },
      { source: "/api/:path*", destination: `${apiOrigin}/api/:path*` },
    ];
  },
};

export default config;
