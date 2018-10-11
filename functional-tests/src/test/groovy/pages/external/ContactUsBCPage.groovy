package pages.external

import geb.Page

class ContactUsBCPage extends Page {

	static at = { title.startsWith("Contact Us") && title.endsWith("Province of British Columbia") }
    static url = "https://forms.gov.bc.ca/"
}
