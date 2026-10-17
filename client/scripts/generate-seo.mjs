import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const output = process.env['SEO_OUTPUT_DIR'] ? path.resolve(process.env['SEO_OUTPUT_DIR']) : path.join(root, 'dist/euroleague-quiz-client/browser');
const pages = JSON.parse(await readFile(path.join(root, 'src/seo-pages.json'), 'utf8'));
const template = await readFile(path.join(output, 'index.html'), 'utf8');
const escape = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const origin = 'https://pickandroll.io';

for (const page of pages) {
  let html = template.replace(/<title>[^<]*<\/title>/i, `<title>${escape(page.title)}</title>`);
  // Replace only managed metadata; retain bundles, styles and analytics from Angular's build.
  html = html.replace(/<meta\b[^>]*(?:name|property)=["'](?:description|og:title|og:description|og:url|twitter:title|twitter:description)["'][^>]*>/gi, '');
  html = html.replace(/<link\b[^>]*rel=["']canonical["'][^>]*>/gi, '');
  const url = origin + page.path;
  const tags = [
    `<meta name="description" content="${escape(page.description)}">`,
    `<meta property="og:title" content="${escape(page.title)}">`,
    `<meta property="og:description" content="${escape(page.description)}">`,
    `<meta property="og:url" content="${url}">`,
    `<meta name="twitter:title" content="${escape(page.title)}">`,
    `<meta name="twitter:description" content="${escape(page.description)}">`,
    `<link rel="canonical" href="${url}">`
  ].join('\n');
  html = html.replace('</head>', `${tags}\n</head>`);
  const directory = path.join(output, page.path.slice(1));
  await mkdir(directory, { recursive: true });
  await writeFile(path.join(directory, 'index.html'), html);
}

await writeFile(path.join(output, 'robots.txt'), `User-agent: *\nAllow: /\nDisallow: /api/\n\nSitemap: ${origin}/sitemap.xml\n`);
await writeFile(path.join(output, 'sitemap.xml'), `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${pages.map(page => `  <url><loc>${origin}${page.path}</loc></url>`).join('\n')}\n</urlset>\n`);
console.log(`SEO: wrote ${pages.length} route HTML files, robots.txt and sitemap.xml.`);
