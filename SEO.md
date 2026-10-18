# Pick & Roll SEO

Production origin: `https://pickandroll.io`.

## Included

- `client/src/seo-pages.json` defines the eleven public URLs, titles and descriptions, including Contact Us.
- Angular's SEO service updates titles, descriptions, canonical URLs and share metadata on navigation. Query strings and fragments are excluded from canonical URLs.
- `npm run build -- --configuration production` automatically runs `postbuild`, generating an HTML shell with unique metadata for every public route, plus `robots.txt` and `sitemap.xml` in the browser output directory.
- Nginx serves these route files directly, redirects `/grid` to `/grid/normal`, and returns HTTP 404 for unknown URLs. API proxying is unchanged.
- The site has basic WebSite structured data, an English language declaration, a favicon and font connection hints.

The generated route files contain metadata and the Angular application shell. They do **not** prerender the page body or game data. Search engines still need to render Angular for the main content. Full prerendering or SSR would be a separate improvement. The metadata approach follows [Google's JavaScript SEO guidance](https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics).

## Deploy and verify

Deploy the updated web image through your existing workflow. The Dockerfile already uses `npm run build`, so the metadata step runs automatically. If you run `ng build` directly, follow it with `node scripts/generate-seo.mjs`.

After deployment:

```bash
curl -I https://pickandroll.io/grid/normal
curl -s https://pickandroll.io/game/mystery-player | head -c 4000
curl https://pickandroll.io/robots.txt
curl https://pickandroll.io/sitemap.xml
curl -I https://pickandroll.io/this-page-does-not-exist
```

Verify the game HTML has its own title, description and canonical URL. The nonexistent URL should return 404. Check a quiz URL after navigating from home as well as after a direct refresh.

Add the domain to Google Search Console, verify ownership through your DNS provider, then submit `https://pickandroll.io/sitemap.xml`. Use URL Inspection on the home page and a game page. No Search Console account changes or sitemap submissions have been performed by this code change.

If using Cloudflare, redirect `www.pickandroll.io` to `https://pickandroll.io` with a Redirect Rule, preserving the request path and query string. The canonical tags already prefer the root domain. Purge old cached HTML after deployment if necessary.

When adding a game route, update `seo-pages.json` and rebuild. Keep descriptions accurate; do not add fabricated ratings, reviews or player statistics to structured data.
