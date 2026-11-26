const api = 'https://localhost:7161';
const web = 'http://localhost:4200';

module.exports = {
  '/api/**': { target: api, changeOrigin: true },
  '/hubs/**': {
    target: api,
    changeOrigin: true,
    ws: true,
    configure(proxy) {
      // Forward only the expected loopback browser origin to the already trusted HTTPS backend.
      const forwardLocalOrigin = (outgoing, incoming) => {
        if (incoming.headers.host === 'localhost:4200' && incoming.headers.origin === web) {
          outgoing.setHeader('origin', api);
        }
      };
      proxy.on('proxyReq', forwardLocalOrigin);
      proxy.on('proxyReqWs', forwardLocalOrigin);
    },
  },
};
