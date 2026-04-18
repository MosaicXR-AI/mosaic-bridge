import { createHmac, createHash, randomUUID } from 'crypto';

function byteLen(s: string): number {
  return Buffer.byteLength(s, 'utf8');
}

export function buildCanonical(
  nonce: string,
  timestamp: string,
  method: string,
  path: string,
  bodySha256: string
): string {
  return [
    'v1',
    `${byteLen(nonce)}:${nonce}`,
    `${byteLen(timestamp)}:${timestamp}`,
    `${byteLen(method)}:${method}`,
    `${byteLen(path)}:${path}`,
    `${byteLen(bodySha256)}:${bodySha256}`,
  ].join('\n');
}

export function computeBodySha256(body: Buffer): string {
  return createHash('sha256').update(body).digest('hex');
}

export function signRequest(
  secretBase64: string,
  method: string,
  path: string,
  body: Buffer
): { nonce: string; timestamp: string; signature: string } {
  const nonce = randomUUID().replace(/-/g, '');
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const bodySha256 = computeBodySha256(body);
  const canonical = buildCanonical(nonce, timestamp, method.toUpperCase(), path, bodySha256);
  const secret = Buffer.from(secretBase64, 'base64');
  const signature = createHmac('sha256', secret)
    .update(Buffer.from(canonical, 'utf8'))
    .digest('hex');
  return { nonce, timestamp, signature };
}
