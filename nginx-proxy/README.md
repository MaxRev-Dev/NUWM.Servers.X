# NGINX Proxy With Let's Encrypt Automation

------

1. Clone this repo:

   ```bash
   git clone https://github.com/MaxRev-Dev/nginx-letsencrypt nginx-proxy
   cd nginx-proxy
   ```

2. Change your container network in `docker-compose.yml`

   ```yaml
   services:
     .... 
      networks:
        - mynetwork <=
     ...
   networks:
      mynetwork:  <=
        external: true
   ```

3. Set environment variables in docker-compose of container to be proxied

   More info: https://github.com/nginx-proxy/docker-letsencrypt-nginx-proxy-companion/wiki

   ```yaml
       - VIRTUAL_HOST=example.com
       - VIRTUAL_PORT={your-container-port} 
       - LETSENCRYPT_HOST=example.com
       - LETSENCRYPT_EMAIL=admin@example.com
       - VIRTUAL_PROTO=https
   ```

4. Start your container

5. Start nginx proxy: `docker-compose up -d --build`

