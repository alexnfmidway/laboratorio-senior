import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

export default function () {
  const res = http.post('http://host.docker.internal:5000/order');
  check(res, {
    'is status 200': (r) => r.status === 200,
  });
  sleep(1);
}
