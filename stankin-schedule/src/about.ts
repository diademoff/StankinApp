import { aboutConfig } from './about-config';

const byId = (id: string) => document.getElementById(id)!;

byId('about-version').textContent = aboutConfig.version;
byId('about-schedule-date').textContent = `Расписание от ${aboutConfig.scheduleDate}`;
byId('about-copyright').textContent = aboutConfig.copyright;
byId('about-email').textContent = aboutConfig.email;
byId('about-email').setAttribute('href', `mailto:${aboutConfig.email}`);
byId('about-pgp-key').textContent = aboutConfig.pgpKey;
byId('about-fingerprint').textContent = aboutConfig.pgpFingerprint;
