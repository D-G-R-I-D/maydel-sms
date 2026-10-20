/**
 * E7: the website's standing copy, carried over from the school's previous site (../website). News,
 * notices, events, gallery, staff and contact details come from the API; this is the text that rarely
 * changes. Contact details are never hard-coded here: they come from Settings → School.
 */

export const siteNav = [
  { href: "/", label: "Home" },
  { href: "/about", label: "About" },
  { href: "/admissions-info", label: "Admissions" },
  { href: "/parents", label: "Parents" },
  { href: "/news", label: "News" },
  { href: "/events", label: "Events" },
  { href: "/gallery", label: "Gallery" },
  { href: "/staff", label: "Staff" },
  { href: "/contact", label: "Contact" },
] as const;

export const hero = {
  title: "Give your child a strong, joyful start.",
  /** The headline split so the promise can be set in gold italics (as on the sign-in panel). */
  titleLead: "Give your child a",
  titleAccent: "strong, joyful start.",
  text: "At Maydel Spring Schools, your child learns, grows and thrives in a safe, nurturing environment, guided by teachers who know them by name and an unwavering commitment to their future.",
};

export const pillars = [
  { title: "Academic excellence", text: "A well-structured curriculum that challenges students, ignites curiosity and builds genuine intellectual confidence." },
  { title: "Character formation", text: "Integrity, respect and compassion are woven into everyday learning, not just taught as abstract values." },
  { title: "Expert educators", text: "Qualified, dedicated teachers who know every child by name and nurture them as individuals." },
  { title: "Exam readiness", text: "An approved WAEC and NECO examination centre: our students are fully prepared for the national stage." },
] as const;

export const programmes = [
  { title: "Creche & Nursery", range: "Ages 1–4", text: "Warm, playful beginnings where curiosity is celebrated and every child feels safe, seen and supported." },
  { title: "Primary School", range: "Year 1–6", text: "Strong academic foundations built through engaging lessons, small classes and genuine teacher attention." },
  { title: "Junior Secondary", range: "Year 7–9", text: "Broad exploration across sciences, arts and languages, helping students discover their strengths." },
  { title: "Senior Secondary", range: "Year 10–12", text: "Focused WAEC and NECO preparation, with examination-centre accreditation right here on campus." },
] as const;

export const story = {
  founded: 2022,
  founder: "Lekwot Chiamaka",
  founderRole: "Proprietress & Founder",
  founderQuote: "Every child who walks through our doors carries a spark. Our job is to help it grow into a flame.",
  paragraphs: [
    "Founded in 2022 by Proprietress Lekwot Chiamaka, Maydel Spring Schools began with a simple goal: to give children a warm, disciplined and academically strong foundation.",
    "In under five years we have grown into a thriving community of students, teachers and families, a testament to the trust families have placed in us and the commitment of our staff.",
    "We are proud to be a fully approved WAEC and NECO examination centre, a milestone that reflects the standards our staff and students hold themselves to every day.",
    "Our commitment to academic excellence, character development and holistic education remains at the heart of everything we do.",
  ],
  vision: "To develop well-rounded, confident and responsible individuals who aspire to achieve their full potential.",
  mission: "To provide a welcoming, happy, safe and supportive learning environment in which everyone is valued and all achievements are celebrated.",
  values: ["Integrity", "Excellence", "Respect", "Compassion", "Innovation", "Discipline"],
};

export const admissionSteps = [
  { title: "Buy the form online", text: "Create a family account and pay for the admission form online, or with a code from the school office." },
  { title: "Fill in the form", text: "Tell us about your child and your family, and upload a passport photograph and supporting documents." },
  { title: "Entrance exam", text: "Book a sitting that suits you. You'll see the outcome in your account." },
  { title: "Offer and enrolment", text: "Accept the offer, and your child is placed in the most suitable class with orientation support." },
] as const;

export const faqs = [
  {
    q: "What age groups do you accept?",
    a: "We welcome children from age 1 through to Year 12, across our Creche, Nursery, Primary, Junior Secondary and Senior Secondary sections. Each level follows an age-appropriate curriculum designed to nurture the whole child.",
  },
  {
    q: "Are you approved for WAEC and NECO?",
    a: "Yes. Maydel Spring Schools is accredited by both the West African Examinations Council (WAEC) and the National Examinations Council (NECO). Our students sit their national examinations on our campus.",
  },
  { q: "How do I apply?", a: "Buy the admission form online from this site, fill it in from your family account, then book the entrance exam. The school office can help at any step." },
  {
    q: "What are the school hours?",
    a: "Creche and Nursery: 7:30 am – 3:30 pm. Primary and Secondary: 7:45 am – 3:45 pm. Early drop-off is available from 7:00 am, and after-school care on request.",
  },
  { q: "How do parents follow their child's progress?", a: "Every family gets a secure portal login to see report cards, attendance, fees and school news at any time." },
] as const;

export const siteImages = {
  classroom: { src: "/site/classroom.jpg", alt: "Pupils learning in a Maydel Spring Schools classroom" },
  aerobics: { src: "/site/aerobics.jpg", alt: "Pupils at a morning exercise session" },
  teaching: { src: "/site/teaching.jpg", alt: "A teacher guiding pupils during a lesson" },
  founder: { src: "/site/founder-photo.jpg", alt: "Lekwot Chiamaka, Proprietress and Founder" },
  campus: { src: "/site/campus.jpg", alt: "The school campus: blue-and-white classroom blocks around the basketball court" },
  courtyard: { src: "/site/courtyard.jpg", alt: "The courtyard, with murals on the classroom walls and the court" },
  playArea: { src: "/site/play-area.jpg", alt: "The shaded early-years play area with soft artificial grass" },
  pitch: { src: "/site/sports-pitch.jpg", alt: "The fenced football pitch and courts" },
  hero: { src: "/site/hero-campus.jpg", alt: "Classroom blocks with red roofs around the school's basketball court" },
  signboard: { src: "/site/signboard.jpg", alt: "The Maydel Spring Schools signboard by the gate: Early years, Primary, Junior High, Senior High, After-school care" },
  earlyYears: { src: "/site/early-years-room.jpg", alt: "The early-years room: a “Small hands change the world” door, toys, a play pen and pictures on the wall" },
  culture: { src: "/site/cultural-heritage.jpg", alt: "The play area set up for the Cultural Heritage celebration" },
  coveredPlay: { src: "/site/covered-play-area.jpg", alt: "The covered play area with soft artificial grass" },
} as const;

/** Short reasons to choose the school, scrolled in a band on the homepage. */
export const highlights = [
  "Child-centred learning",
  "WAEC & NECO approved",
  "Qualified teaching staff",
  "Small class sizes",
  "Character development",
  "Creative arts & sports",
  "Safe, nurturing environment",
  "24/7 CCTV security",
  "School bus service",
] as const;

/** Families' words from the school's previous site. */
export const testimonials = [
  { quote: "The teachers know my daughter as an individual. She's grown so much in confidence, not just academically but as a person.", by: "Parent of a Year 3 pupil", section: "Primary" },
  { quote: "We chose Maydel because it feels like a family. The environment is calm, safe and truly child-focused. Our son looks forward to school every morning.", by: "Parent of a Year 8 student", section: "Junior Secondary" },
  { quote: "The standard of teaching is exceptional. My daughter passed her WAEC with flying colours, and she actually enjoyed the process.", by: "Parent of an SSS 3 graduate", section: "Senior Secondary" },
] as const;

export const anniversary = { years: 5, when: "March 2027" } as const;

// Website photos never show identifiable children unless the school confirms parents' consent for
// website use (e.g. the old site's assembly crowd and single-child close-up are deliberately left out).

/** The parents' guide (the previous site's "For Parents" page). */
export const parentGuide = [
  {
    id: "hours",
    title: "School hours",
    text: "When the school day starts and ends for each section.",
    points: ["Creche & Nursery: 7:30 am – 3:30 pm", "Primary: 7:45 am – 3:45 pm", "Secondary: 7:45 am – 3:45 pm", "Early drop-off from 7:00 am", "After-school care on request"],
  },
  {
    id: "transport",
    title: "School bus",
    text: "Our bus service makes daily drop-off and pick-up convenient and safe.",
    points: ["Trained drivers and bus attendants", "GPS-tracked vehicles", "Selected routes across Abuja", "Ask the office about routes and availability"],
  },
  {
    id: "security",
    title: "Safety and security",
    text: "Your child's safety is our first priority; the campus is monitored around the clock.",
    points: ["24/7 CCTV on all key areas", "Fully fenced, gated campus", "Verified pick-up only", "Trained staff and attendants on site", "First-aid trained personnel"],
  },
  {
    id: "uniform",
    title: "Uniform",
    text: "Comfortable, durable and available at the school.",
    points: ["Full sets: everyday and sports", "Sold at the school office", "Sizes from Creche to SSS", "Shoes, socks and accessories in the pack"],
  },
  {
    id: "meals",
    title: "Meals and snacks",
    text: "Nutritious, age-appropriate snacks and meals through the day.",
    points: ["Mid-morning snack", "Lunch for full-day pupils", "Afternoon snack in after-school care", "Allergies accommodated with notice"],
  },
  {
    id: "clubs",
    title: "Clubs and activities",
    text: "Learning beyond the classroom.",
    points: ["Ballet and hip-hop", "Coding and ICT", "Music and choir", "Sports: football and athletics", "Creative arts"],
  },
  {
    id: "curriculum",
    title: "Curriculum",
    text: "A Nigerian-British curriculum, with Montessori principles through hands-on, child-centred learning.",
    points: ["English, Mathematics, Science and ICT", "Creative and cultural arts", "History and national values", "French from Primary", "Pre-vocational studies"],
  },
  {
    id: "policies",
    title: "Key policies",
    text: "Full policy documents are available from the school office.",
    points: ["Attendance and punctuality", "Behaviour and discipline", "Pick-up and drop-off", "Mobile phones", "Sick children"],
  },
] as const;

export const parentFaqs = [
  { q: "What should my child bring on the first day?", a: "A filled water bottle, a spare set of clothes for younger children, and a healthy snack. Books, stationery and learning materials are provided by the school." },
  {
    q: "How do I talk to my child's teacher?",
    a: "Through the school office, the parent portal, or the parent–teacher meetings each term. For anything urgent, call the school office directly.",
  },
  { q: "What if my child is sick?", a: "Call or message the office before 8:00 am so we can note the absence. With a contagious illness, please keep your child at home until a doctor clears them." },
  {
    q: "Who can pick up my child?",
    a: "Only parents, guardians and adults on your child's pick-up authorisation form. Anyone else must be arranged with the office in advance and show ID. This is non-negotiable for your child's safety.",
  },
  { q: "How do I see results, fees and attendance?", a: "Sign in to the parent portal with the account the school gives you: report cards, attendance, fees and receipts, and school notices are all there." },
] as const;
