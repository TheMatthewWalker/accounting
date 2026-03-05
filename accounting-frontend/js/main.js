// Main application initialization

import { ApiService, UIUtils } from './api.js';

document.addEventListener('DOMContentLoaded', () => {
    // Smooth scroll for navigation links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });

    // Check if user is logged in
    if (ApiService.isLoggedIn()) {
        // Redirect to dashboard if on landing page
        if (window.location.pathname === '/' || window.location.pathname === '/index.html') {
            window.location.href = '/pages/dashboard.html';
        }
    }
});

// Navigation highlight
function highlightCurrentNav() {
    const pathname = window.location.pathname;
    document.querySelectorAll('.navbar-nav a').forEach(link => {
        if (link.getAttribute('href') === pathname) {
            link.classList.add('active');
        } else {
            link.classList.remove('active');
        }
    });
}

window.addEventListener('load', highlightCurrentNav);
